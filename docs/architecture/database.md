# Thiết kế dữ liệu — Hệ thống đặt sân cầu lông

**Phiên bản 1.5 · 10/10/2026**

Căn cứ: SRS 1.5 và Use Case 1.4. Mã BR tham chiếu SRS.

## Mục lục

1. [Quy ước chung](#1-quy-ước-chung)
2. [Tài khoản và người dùng](#2-tài-khoản-và-người-dùng)
3. [Quản lý sân, lịch và giá](#3-quản-lý-sân-lịch-và-giá)
4. [Đặt sân và lịch sử](#4-đặt-sân-và-lịch-sử)
5. [Hóa đơn và hoàn tiền](#5-hóa-đơn-và-hoàn-tiền)
6. [Nhật ký, chống gửi lặp và thông báo](#6-nhật-ký-chống-gửi-lặp-và-thông-báo)
7. [Toàn vẹn dữ liệu và giao dịch](#7-toàn-vẹn-dữ-liệu-và-giao-dịch)
8. [Tra cứu, thống kê và đối chiếu Use Case](#8-tra-cứu-thống-kê-và-đối-chiếu-use-case)

## 1. Quy ước chung

| Nội dung            | Quy ước                                                                                                                                                                                                                                              |
| ------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Khóa                | Giữ UUID cho tài khoản, hồ sơ, đặt sân, hóa đơn; số nguyên cho sân/lịch/giá hiện hành.                                                                                                                                                               |
| Trường bắt buộc     | Trường không ghi `NULL` là `NOT NULL`. FK đến dữ liệu lịch sử dùng `ON DELETE RESTRICT`, không cascade xóa giao dịch. Ngoại lệ: FK hồ sơ → UserAccount dùng `ON DELETE SET NULL` để hard-delete tài khoản và giữ hồ sơ.                              |
| Thời điểm           | `DateTime UTC` → PostgreSQL `timestamptz`; ứng dụng ghi/đọc UTC. Không dùng giờ địa phương để lưu `createdAt`, `paidAt`, thời điểm nhật ký hoặc hết hạn mã.                                                                                          |
| Ngày và giờ sử dụng | `DateOnly` → `date`; `TimeOnly` → `time without time zone`, diễn giải theo giờ Việt Nam. `date + startTime/endTime` là giờ địa phương của lượt sử dụng; chuyển sang UTC khi so với đồng hồ máy chủ.                                                  |
| Ngày trong tuần     | ISO-8601: 1 = Thứ Hai, …, 7 = Chủ Nhật.                                                                                                                                                                                                              |
| Đơn vị tiền         | VND. Đơn giá, tổng tiền và tiền hoàn dùng `Decimal(18,0)` → `numeric(18,0)`; kiểm tra đầu vào nguyên đồng trước khi lưu, không để DB tự làm tròn đầu vào sai. Tính các phần bằng decimal đủ độ chính xác, cộng rồi làm tròn tổng một lần theo BR-04. |
| Giờ mở và giờ đặt   | Không qua nửa đêm; giờ mở/đóng, bắt đầu/kết thúc ở phút 00 hoặc 30, giây bằng 0. Mốc giá chính xác đến phút; SRS không bắt buộc mốc giá ở phút 00/30.                                                                                                |
| Email               | Trim và chuyển chữ thường; kiểm tra định dạng, không gộp alias/dấu chấm/phần sau dấu cộng. Lưu giá trị chuẩn hóa để so sánh duy nhất.                                                                                                                |
| Số điện thoại       | Chuỗi; bỏ dấu cách/dấu gạch ngang, cho phép `+` đầu chuỗi, từ 8–15 chữ số. Không UNIQUE, không dùng để tự gộp hồ sơ.                                                                                                                                 |
| Cập nhật đồng thời  | Giao dịch, khóa bản ghi và kiểm tra lại dữ liệu gốc/trạng thái theo BR-21.                                                                                                                                                                           |
| Enum                | Tên nghiệp vụ/API viết hoa; giữ cách lưu tên PascalCase của bản gốc trong DB. Ví dụ `CONFIRMED` ↔ `Confirmed`, `PAID` ↔ `Paid`. SQL minh họa dùng giá trị DB PascalCase và tên bảng/cột có dấu ngoặc kép.                                            |

SQL mẫu; ràng buộc liên bảng triển khai theo mục 7.

## 2. Tài khoản và người dùng

### 2.1 Quy tắc nghiệp vụ

- **BR-10, BR-11:** Một tài khoản liên kết đúng một hồ sơ Khách hàng hoặc Nhân viên, không cả hai; mỗi hồ sơ tối đa một tài khoản. Nhân viên/QTV không sở hữu đặt sân.
- **BR-10, BR-11:** Tái sử dụng hồ sơ khách theo email, giữ mã khách và lịch sử, không ghi đè họ tên/điện thoại hồ sơ cũ từ form đặt nhanh/đăng ký. Đăng ký công khai chỉ tạo tài khoản sau xác minh thành công.
- **BR-12:** Khóa/vô hiệu hóa cùng dùng DISABLED; khóa ở lần sai thứ 5, chỉ người có quyền mở về ACTIVE, không tự mở theo thời gian/đặt lại mật khẩu. Khóa không hủy giao dịch.
- **BR-18, BR-26, BR-27:** Đổi email đúng quyền, xác minh email mới, đồng bộ email hồ sơ/email đăng nhập, vô hiệu mã xác minh email cũ; không đổi loại tài khoản Khách hàng ↔ Nhân viên.
- **BR-27:** QTV cấp tài khoản nội bộ và thay quyền Nhân viên ↔ QTV. Bảo vệ QTV Hoạt động cuối trước thao tác quản trị; khóa tự động vẫn áp dụng ở lần sai thứ 5, phục hồi theo quy trình vận hành có dấu vết.

### 2.2 Entities

**UserAccount** — Tài khoản đăng nhập

| Field               | Type                | Ghi chú                                                       |
| ------------------- | ------------------- | ------------------------------------------------------------- |
| accountId           | uuid                | PK                                                            |
| loginEmail          | string              | Email chuẩn hóa, UNIQUE                                       |
| passwordHash        | string              | Chỉ lưu băm mật khẩu                                          |
| accountType         | Enum[AccountType]   | CUSTOMER hoặc EMPLOYEE; không đổi loại sau tạo                |
| status              | Enum[AccountStatus] | ACTIVE hoặc DISABLED (khóa/vô hiệu hóa)                       |
| failedLoginAttempts | int                 | 0–5, số lần sai mật khẩu liên tiếp; không gộp sai mã xác minh |
| createdAt           | DateTime UTC        |                                                               |
| updatedAt           | DateTime UTC        |                                                               |

Sai mật khẩu 5 lần hoặc khóa thủ công đều chuyển DISABLED. Nhân viên/QTV mở khóa theo quyền, chuyển ACTIVE và reset bộ đếm về 0. Không tự mở theo thời gian hoặc đặt lại mật khẩu.

Không có bảng Session. Mỗi yêu cầu cần đăng nhập kiểm tra tài khoản tồn tại, ACTIVE và quyền hiện tại phía máy chủ.

**Employee** — Hồ sơ nhân viên/QTV

| Field      | Type         | Ghi chú                                                                                                                            |
| ---------- | ------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| employeeId | uuid         | PK                                                                                                                                 |
| accountId  | uuid, NULL   | FK → UserAccount.accountId, UNIQUE khi có; ON DELETE SET NULL. Khi tạo nhân viên phải có tài khoản; NULL sau hard-delete tài khoản |
| fullName   | string       | Bắt buộc, không chỉ khoảng trắng                                                                                                   |
| phone      | string       | Theo BR-26; không UNIQUE                                                                                                           |
| email      | string       | Bắt buộc, chuẩn hóa, UNIQUE, đồng bộ loginEmail                                                                                    |
| isAdmin    | bool         | Quyền QTV trên hồ sơ Nhân viên                                                                                                     |
| createdAt  | DateTime UTC |                                                                                                                                    |
| updatedAt  | DateTime UTC |                                                                                                                                    |

Khóa tài khoản nhân viên cập nhật DISABLED; xóa tài khoản là hard-delete UserAccount, Employee.accountId thành NULL và giữ hồ sơ cùng mã nhân viên. Hồ sơ không còn tài khoản không có quyền đăng nhập; isAdmin không tự cấp quyền khi không có tài khoản ACTIVE.

**Customer** — Hồ sơ khách hàng

| Field      | Type         | Ghi chú                                                               |
| ---------- | ------------ | --------------------------------------------------------------------- |
| customerId | uuid         | PK                                                                    |
| accountId  | uuid, NULL   | FK → UserAccount.accountId, UNIQUE khi có giá trị, ON DELETE SET NULL |
| fullName   | string       | Bắt buộc, không chỉ khoảng trắng                                      |
| phone      | string       | Theo BR-26; không UNIQUE                                              |
| email      | string       | Chuẩn hóa, UNIQUE kể cả khách chưa có tài khoản                       |
| createdAt  | DateTime UTC |                                                                       |
| updatedAt  | DateTime UTC |                                                                       |

Đặt nhanh bằng email đã có tài khoản vẫn dùng hồ sơ đó sau xác minh đúng mục đích; không cấp phiên hay quyền xem lịch sử. Không tạo hồ sơ trùng email và không dùng số điện thoại để tự gộp hồ sơ.

**VerificationCode** — Mã xác minh hiện tại theo email và mục đích

| Field     | Type           | Ghi chú                                |
| --------- | -------------- | -------------------------------------- |
| email     | string         | PK thành phần; email chuẩn hóa         |
| type      | Enum[CodeType] | PK thành phần; mục đích xác minh       |
| codeHash  | string         | Băm mã 6 chữ số                        |
| attempt   | int            | Số lần nhập sai, 0–5                   |
| isUsed    | bool           | Đã tiêu thụ trong giao dịch thành công |
| expiresAt | DateTime UTC   | Hết hạn sau 10 phút                    |

Mỗi `(email, type)` giữ một mã. Gửi lại thay mã cũ, đặt attempt = 0 và isUsed = false. Chỉ nhận mã đúng, còn hạn, chưa dùng và attempt < 5. Khóa bản ghi khi kiểm tra/tiêu thụ mã; lưu nghiệp vụ và đánh dấu isUsed cùng giao dịch, lỗi thì hoàn tác. Vô hiệu mã bằng cách đặt expiresAt về thời điểm hiện tại.

Giới hạn gửi và ngữ cảnh xác minh đặt nhanh lưu tạm trong cache phía máy chủ theo BR-26; không thêm bảng lịch sử gửi. Bộ đếm gửi giữ đủ cửa sổ 1 giờ, độc lập với mã hiện tại. Ngữ cảnh gắn email, mục đích, mã hiện tại và nội dung đặt; đổi nội dung phải xác minh lại. Gửi lỗi không kích hoạt mã mới hoặc báo thành công.

### 2.3 Hard-delete tài khoản

QTV xóa UserAccount trong một giao dịch: kiểm tra quyền và bảo vệ QTV ACTIVE cuối; vô hiệu mã xác minh theo loginEmail của tài khoản; đặt Customer/Employee.accountId NULL qua FK SET NULL; hard-delete UserAccount; ghi Audit xóa với actor là hồ sơ nhân viên và entityId là accountId đã xóa. Không cascade xóa hồ sơ hoặc giao dịch. Audit, BookingStatusHistory và Invoice tham chiếu employeeId/customerId, không tham chiếu bắt buộc tới tài khoản đích đã xóa. Các yêu cầu dùng accountId không còn tồn tại bị từ chối, kể cả nếu trình duyệt còn cookie cũ.

IdempotencyRequest.actorScope là định danh đã lưu dạng chuỗi, không có FK UserAccount; giữ kết quả gửi lặp cũ để bảo toàn dấu vết nhưng không trả kết quả cho người không còn quyền/tài khoản. Không có cơ chế mở khóa để khôi phục bản ghi tài khoản đã hard-delete.

### 2.4 Constraints và enums

```sql
CREATE UNIQUE INDEX "UX_UserAccount_LoginEmail"
  ON "UserAccount" ("loginEmail");
CREATE UNIQUE INDEX "UX_Employee_Account" ON "Employee" ("accountId");
CREATE UNIQUE INDEX "UX_Employee_Email" ON "Employee" ("email");
CREATE UNIQUE INDEX "UX_Customer_Account" ON "Customer" ("accountId")
  WHERE "accountId" IS NOT NULL;
CREATE UNIQUE INDEX "UX_Customer_Email" ON "Customer" ("email");
ALTER TABLE "Employee" ADD CONSTRAINT "FK_Employee_UserAccount"
  FOREIGN KEY ("accountId") REFERENCES "UserAccount" ("accountId") ON DELETE SET NULL;
ALTER TABLE "Customer" ADD CONSTRAINT "FK_Customer_UserAccount"
  FOREIGN KEY ("accountId") REFERENCES "UserAccount" ("accountId") ON DELETE SET NULL;
ALTER TABLE "VerificationCode" ADD CONSTRAINT "PK_VerificationCode"
  PRIMARY KEY ("email", "type");
```

- CHECK email đã trim/chữ thường và không rỗng; họ tên không chỉ khoảng trắng; số điện thoại theo BR-26.
- CHECK `failedLoginAttempts BETWEEN 0 AND 5`; bộ đếm bằng 5 buộc status = Disabled.
- CHECK status chỉ Active hoặc Disabled; khi mở khóa reset bộ đếm về 0. Khóa thủ công không yêu cầu bộ đếm bằng 5.
- CHECK `attempt BETWEEN 0 AND 5`; dịch vụ chỉ nhận mã khi attempt < 5, isUsed = false và expiresAt > thời điểm máy chủ.
- UNIQUE trên hai bảng hồ sơ **không đủ** để bảo đảm một tài khoản chỉ thuộc đúng một loại hồ sơ hoặc đồng bộ email; dùng constraint trigger kiểm tra cuối giao dịch theo mục 7; hồ sơ sau khi hard-delete tài khoản được phép accountId NULL.

| Enum          | Values nghiệp vụ                                                            |
| ------------- | --------------------------------------------------------------------------- |
| AccountStatus | `ACTIVE`, `DISABLED`                                                        |
| AccountType   | `CUSTOMER`, `EMPLOYEE`                                                      |
| CodeType      | `VERIFY_EMAIL` (đăng ký), `RESET_PASSWORD`, `QUICK_BOOKING`, `CHANGE_EMAIL` |

CodeType giữ các giá trị số cũ; thêm mục đích mới ở cuối enum.

## 3. Quản lý sân, lịch và giá

### 3.1 Quy tắc nghiệp vụ

- **BR-01, BR-25:** Mỗi sân đúng 7 lịch, một lịch/ngày trong tuần. Tạo sân cùng 7 lịch và 7 mốc giá mặc định; ngày nghỉ đánh dấu không khả dụng, không xóa lịch.
- **BR-03:** Giá theo mốc giờ, không lưu giờ kết thúc riêng. Mốc đầu bằng giờ mở, mọi mốc trước giờ đóng, đơn giá nguyên VND > 0; bảng giá phải phủ toàn bộ giờ mở kể cả ngày nghỉ.
- **BR-16, BR-19:** Đóng/bảo trì/xóa sân cần xử lý hết đơn PENDING/CONFIRMED. Sửa lịch không làm các đơn bị ảnh hưởng ra ngoài giờ mở hoặc mất khả dụng; trong ngày không loại các lượt COMPLETED đã ghi nhận khỏi giờ mở.
- **BR-19:** Xóa mềm chuyển DELETED và name = NULL; giữ sân/lịch/giá/giao dịch, không khôi phục/sửa sân đã xóa. Tên cũ lưu trong Audit.
- **BR-25:** Giữ giá đã áp dụng trên từng đơn theo BR-04.

### 3.2 Entities hiện hành

**BadmintonCourt**

| Field       | Type               | Ghi chú                                                                                |
| ----------- | ------------------ | -------------------------------------------------------------------------------------- |
| courtId     | int                | PK                                                                                     |
| name        | string, NULL       | NULL chỉ khi DELETED; tên chưa xóa trim, bắt buộc, duy nhất không phân biệt hoa/thường |
| description | string             |                                                                                        |
| status      | Enum[CourtStatus]  |                                                                                        |
| deletedAt   | DateTime UTC, NULL | Mốc xóa; xác định ngày xóa theo giờ Việt Nam khi thống kê                              |
| createdAt   | DateTime UTC       |                                                                                        |
| updatedAt   | DateTime UTC       |                                                                                        |

**CourtSchedule** — Một lịch hiện hành mỗi sân/ngày trong tuần

| Field       | Type         | Ghi chú                                      |
| ----------- | ------------ | -------------------------------------------- |
| scheduleId  | int          | PK                                           |
| courtId     | int          | FK → BadmintonCourt.courtId                  |
| dayOfWeek   | int          | 1–7                                          |
| openTime    | TimeOnly     | Phút 00/30, giây 0                           |
| closeTime   | TimeOnly     | Phút 00/30, giây 0; openTime < closeTime     |
| isAvailable | bool         | false cho ngày nghỉ; vẫn giữ giờ và bảng giá |
| createdAt   | DateTime UTC |                                              |
| updatedAt   | DateTime UTC |                                              |

**PricingRule** — Các mốc giá hiện hành

| Field         | Type          | Ghi chú                                             |
| ------------- | ------------- | --------------------------------------------------- |
| pricingRuleId | int           | PK                                                  |
| courtId       | int           | FK → BadmintonCourt.courtId                         |
| dayOfWeek     | int           | 1–7; FK ghép `(courtId, dayOfWeek)` → CourtSchedule |
| startTime     | TimeOnly      | Mốc bắt đầu; chính xác đến phút, giây 0             |
| pricePerHour  | Decimal(18,0) | Nguyên đồng VND, > 0                                |
| createdAt     | DateTime UTC  |                                                     |
| updatedAt     | DateTime UTC  |                                                     |

Không thêm `endTime` vào PricingRule. Khi thay toàn bộ bảng giá, khóa lịch tương ứng, kiểm tra lại lịch/giá hiện hành, thay danh sách mốc và ghi Audit trong cùng giao dịch. Khóa dòng lịch bảo vệ cả cấu hình lịch/giá của ngày đó.

### 3.3 Constraints

```sql
CREATE UNIQUE INDEX "UX_BadmintonCourt_Name"
  ON "BadmintonCourt" (lower(btrim("name"))) WHERE "name" IS NOT NULL;
ALTER TABLE "BadmintonCourt" ADD CONSTRAINT "CK_Court_DeletedName"
  CHECK (
    ("status" = 'Deleted' AND "name" IS NULL AND "deletedAt" IS NOT NULL)
    OR
    ("status" <> 'Deleted' AND "name" IS NOT NULL
     AND "name" = btrim("name") AND length("name") > 0 AND "deletedAt" IS NULL)
  );
ALTER TABLE "CourtSchedule" ADD CONSTRAINT "UQ_CourtSchedule_Day"
  UNIQUE ("courtId", "dayOfWeek");
ALTER TABLE "CourtSchedule" ADD CONSTRAINT "CK_CourtSchedule_Hours"
  CHECK (
    "dayOfWeek" BETWEEN 1 AND 7 AND "openTime" < "closeTime"
    AND EXTRACT(SECOND FROM "openTime") = 0
    AND EXTRACT(SECOND FROM "closeTime") = 0
    AND EXTRACT(MINUTE FROM "openTime") IN (0, 30)
    AND EXTRACT(MINUTE FROM "closeTime") IN (0, 30)
  );
ALTER TABLE "PricingRule" ADD CONSTRAINT "UQ_PricingRule_Milestone"
  UNIQUE ("courtId", "dayOfWeek", "startTime");
ALTER TABLE "PricingRule" ADD CONSTRAINT "FK_PricingRule_Schedule"
  FOREIGN KEY ("courtId", "dayOfWeek")
  REFERENCES "CourtSchedule" ("courtId", "dayOfWeek") ON DELETE RESTRICT;
ALTER TABLE "PricingRule" ADD CONSTRAINT "CK_PricingRule_Value"
  CHECK ("dayOfWeek" BETWEEN 1 AND 7 AND "pricePerHour" > 0
         AND EXTRACT(SECOND FROM "startTime") = 0);
```

Không dùng exclusion constraint trên các khoảng CourtSchedule: mỗi ngày chỉ một lịch. Không dùng `tsrange(openTime, closeTime)` vì `tsrange` cần timestamp, không nhận hai giá trị time.

- Kiểm tra một sân đủ 7 lịch, mỗi bảng giá không rỗng, mốc đầu bằng giờ mở, các mốc thuộc `[openTime, closeTime)` tại cuối giao dịch; UNIQUE chỉ bảo đảm tối đa một lịch/ngày.

| Enum        | Values                                       |
| ----------- | -------------------------------------------- |
| CourtStatus | `ACTIVE`, `MAINTENANCE`, `CLOSED`, `DELETED` |

## 4. Đặt sân và lịch sử

### 4.1 Quy tắc nghiệp vụ

- **BR-02:** Tất cả trạng thái trừ CANCELLED giữ chỗ, kể cả COMPLETED; khoảng thời gian `[startTime, endTime)` cho phép hai lượt nối tiếp.
- **BR-04:** Lưu giá khi tạo, chỉ tính lại khi đổi sân/ngày/giờ. Xác nhận/Hoàn thành không tính lại giá; giá xem trước không giữ chỗ.
- **BR-05, BR-20:** Tạo thông thường trước giờ bắt đầu; hủy/đổi theo yêu cầu khách tại mốc cách giờ bắt đầu ít nhất 1 giờ, đúng mốc vẫn được. Lượt mới khi đổi cũng cách ít nhất 1 giờ.
- **BR-06:** CONFIRMED là nhân viên xác nhận, COMPLETED là ghi nhận sử dụng hoặc chốt tự động; cả hai độc lập thanh toán.
- **BR-07:** Mã đặt sân tự sinh dạng DS-{chữ và số}, đuôi ít nhất 5 ký tự; ràng buộc duy nhất bảo vệ khi tạo đồng thời.
- **BR-13:** Tạo/đổi trạng thái ghi lịch sử đúng tác nhân trong cùng giao dịch; không suy ra hệ thống chỉ vì employeeId NULL.
- **BR-17, BR-20:** Mọi đơn mới khởi tạo PENDING trước giờ bắt đầu, kể cả nhân viên tạo thay khách. Ghi Hoàn thành thủ công chỉ cho đơn đã tồn tại sau khi lượt thực tế kết thúc; giữ giá snapshot. Chốt tự động qua ngày ghi tác nhân SYSTEM và lý do riêng; đơn cuối không hồi sinh.

### 4.2 Entities

**Booking**

| Field       | Type                | Ghi chú                                                   |
| ----------- | ------------------- | --------------------------------------------------------- |
| bookingId   | uuid                | PK                                                        |
| bookingCode | string              | UNIQUE; DS-{chữ và số}, đuôi ít nhất 5 ký tự              |
| customerId  | uuid                | FK → Customer; bắt buộc, không đổi chủ hồ sơ khi đổi lịch |
| courtId     | int                 | FK → BadmintonCourt                                       |
| date        | DateOnly            | Ngày sử dụng theo Việt Nam                                |
| startTime   | TimeOnly            | Phút 00/30, giây 0                                        |
| endTime     | TimeOnly            | Phút 00/30, giây 0; > startTime, tối thiểu 30 phút        |
| status      | Enum[BookingStatus] |                                                           |
| totalCost   | Decimal(18,0)       | Tổng giá đã làm tròn một lần, > 0; snapshot               |
| createdAt   | DateTime UTC        | Thời điểm tạo thực tế, trước giờ bắt đầu lượt sử dụng     |
| updatedAt   | DateTime UTC        |                                                           |

**BookingPriceSegment** — Chi tiết giá đã áp dụng, snapshot theo đơn

| Field            | Type          | Ghi chú                                                      |
| ---------------- | ------------- | ------------------------------------------------------------ |
| bookingId        | uuid          | PK thành phần, FK → Booking                                  |
| segmentNo        | int           | PK thành phần; thứ tự phần tính tiền                         |
| priceStartTime   | TimeOnly      | Snapshot mốc giá nguồn, không FK tới bảng giá có thể bị thay |
| segmentStartTime | TimeOnly      | Bắt đầu phần thời gian thực chịu giá                         |
| segmentEndTime   | TimeOnly      | Kết thúc phần thời gian thực chịu giá                        |
| pricePerHour     | Decimal(18,0) | Snapshot đơn giá, > 0                                        |

Mỗi phần lưu độc lập mốc giá và đơn giá đã áp dụng từ PricingRule hiện hành tại lúc tạo/đổi lịch. Các phần phủ liên tục đúng khoảng đặt, không chồng/thiếu; tổng tiền = làm tròn một lần tổng `số phút × pricePerHour / 60`. Không làm tròn từng phần. Không FK tới PricingRule hiện hành vì danh sách mốc có thể bị thay; không cần bảng phiên bản giá để giữ giá đã áp dụng trên đơn.

Tạo lưu Booking và các segment trong cùng giao dịch. Đổi lịch thay snapshot bằng các phần giá mới, giữ giá cũ trong Audit; không tính lại khi giữ nguyên sân/ngày/giờ hoặc chỉ xác nhận/Hoàn thành. BookingPriceSegment có giờ kết thúc **phần sử dụng**, không phải endTime của cấu hình PricingRule.

**BookingStatusHistory**

| Field      | Type                      | Ghi chú                                                              |
| ---------- | ------------------------- | -------------------------------------------------------------------- |
| id         | uuid                      | PK                                                                   |
| bookingId  | uuid                      | FK → Booking                                                         |
| oldStatus  | Enum[BookingStatus], NULL | NULL duy nhất ở sự kiện tạo đơn                                      |
| newStatus  | Enum[BookingStatus]       |                                                                      |
| actorType  | Enum[ActorType]           | SYSTEM, EMPLOYEE, CUSTOMER                                           |
| employeeId | uuid, NULL                | FK → Employee, có khi actorType = EMPLOYEE                           |
| customerId | uuid, NULL                | FK → Customer, có khi actorType = CUSTOMER                           |
| changedAt  | DateTime UTC              | Thời điểm thực ghi nhận                                              |
| reason     | string                    | Lý do/ngữ cảnh chuyển trạng thái; chốt ngày có lý do tự động rõ ràng |

CHECK tác nhân: SYSTEM có cả hai mã NULL; EMPLOYEE chỉ có employeeId; CUSTOMER chỉ có customerId. Khách xác minh đặt nhanh được ghi CUSTOMER với hồ sơ gắn đơn, không đồng nghĩa có phiên đăng nhập. Nhân viên tạo thay khách ghi EMPLOYEE. Ghi nhận thủ công và chốt tự động phân biệt bằng actorType/reason/thời điểm. Không ghi `oldStatus = newStatus` vào bảng chuyển trạng thái; đổi lịch giữ nguyên trạng thái được ghi vào Audit cùng các giá trị trước/sau, không tạo chuyển trạng thái giả.

### 4.3 Constraints

```sql
-- Cần btree_gist để GiST hỗ trợ toán tử = trên courtId và date.
CREATE EXTENSION IF NOT EXISTS btree_gist;
CREATE UNIQUE INDEX "UX_Booking_Code" ON "Booking" ("bookingCode");
ALTER TABLE "Booking" ADD CONSTRAINT "CK_Booking_Time"
  CHECK (
    "startTime" < "endTime" AND "endTime" - "startTime" >= INTERVAL '30 minutes'
    AND EXTRACT(SECOND FROM "startTime") = 0
    AND EXTRACT(SECOND FROM "endTime") = 0
    AND EXTRACT(MINUTE FROM "startTime") IN (0, 30)
    AND EXTRACT(MINUTE FROM "endTime") IN (0, 30)
    AND "totalCost" > 0
  );
ALTER TABLE "Booking" ADD CONSTRAINT "CK_Booking_Code"
  CHECK ("bookingCode" ~ '^DS-[A-Za-z0-9]{5,}$');
ALTER TABLE "Booking" ADD CONSTRAINT "EX_Booking_NoOverlap"
  EXCLUDE USING gist (
    "courtId" WITH =,
    "date" WITH =,
    (tsrange("date" + "startTime", "date" + "endTime", '[)')) WITH &&
  ) WHERE ("status" <> 'Cancelled');
CREATE UNIQUE INDEX "UX_BookingHistory_Creation"
  ON "BookingStatusHistory" ("bookingId") WHERE "oldStatus" IS NULL;
ALTER TABLE "BookingStatusHistory" ADD CONSTRAINT "CK_BookingHistory_Actor"
  CHECK (
    ("actorType" = 'System' AND "employeeId" IS NULL AND "customerId" IS NULL)
    OR ("actorType" = 'Employee' AND "employeeId" IS NOT NULL AND "customerId" IS NULL)
    OR ("actorType" = 'Customer' AND "employeeId" IS NULL AND "customerId" IS NOT NULL)
  );
```

Mốc tương lai, giờ kết thúc đã qua, giờ mở cửa, chuyển trạng thái hợp lệ và quyền tác nhân kiểm tra dưới khóa giao dịch bằng đồng hồ máy chủ; không đưa `now()` vào CHECK để giả bảo đảm quy tắc thay đổi theo thời gian. Exclusion constraint là lớp chống trùng cuối cùng, không thay kiểm tra khả dụng hoặc quyền.

| Enum          | Values                                           |
| ------------- | ------------------------------------------------ |
| BookingStatus | `PENDING`, `CONFIRMED`, `COMPLETED`, `CANCELLED` |

## 5. Hóa đơn và hoàn tiền

### 5.1 Quy tắc nghiệp vụ

- **BR-08, BR-09:** PENDING chưa có hóa đơn. CONFIRMED/COMPLETED có đúng một hóa đơn hợp lệ UNPAID hoặc PAID; CANCELLED không có hóa đơn hợp lệ. Hóa đơn CANCELLED vẫn lưu để tra cứu.
- **BR-08:** Xác nhận hoặc ghi nhận trực tiếp Hoàn thành lập hóa đơn theo việc thu thực tế. CONFIRMED → COMPLETED giữ hóa đơn hiện có, không tự đổi thanh toán.
- **BR-09:** Mã HD-{yyyyMMdd}-{số thứ tự}, ngày lập Việt Nam, tăng trong ngày, không dùng lại sau hủy. Nội dung hóa đơn bất biến; chỉ bổ sung thông tin thu/hoàn/hủy và trạng thái đúng quy tắc.
- **BR-22:** Đổi đơn CONFIRMED chưa trả tiền hủy hóa đơn cũ và lập thay thế theo giá mới, dù giá mới bằng giá cũ. Hủy đơn chưa thu đồng thời hủy UNPAID nếu có.
- **BR-23:** Đơn đã thu đổi cùng tổng tiền giữ hóa đơn PAID. Hủy hoặc đổi khác giá phải hoàn đủ ngoài hệ thống, ghi nhận hoàn rồi hủy hóa đơn cũ; đổi khác giá tạo hóa đơn mới. Không chuyển khoản thu cũ sang hóa đơn mới.

### 5.2 Entities

**Invoice**

| Field                 | Type                      | Ghi chú                                                        |
| --------------------- | ------------------------- | -------------------------------------------------------------- |
| invoiceId             | uuid                      | PK                                                             |
| invoiceCode           | string                    | UNIQUE; HD-{yyyyMMdd}-{số thứ tự}                              |
| bookingId             | uuid                      | FK → Booking                                                   |
| totalCost             | Decimal(18,0)             | Bằng giá đã lưu của Booking tại lần lập; không nhập tổng tùy ý |
| status                | Enum[InvoiceStatus]       |                                                                |
| issuedByEmployee      | uuid                      | FK → Employee; nhân viên/QTV khởi tạo thao tác lập             |
| issuedAt              | DateTime UTC              | Thời điểm lập thực tế                                          |
| paidByEmployee        | uuid, NULL                | FK → Employee; người ghi nhận thu đủ                           |
| paidAt                | DateTime UTC, NULL        | Thời điểm thực thu do nhân viên xác nhận                       |
| paymentMethod         | Enum[PaymentMethod], NULL | CASH hoặc BANK                                                 |
| cancelledByEmployee   | uuid, NULL                | FK → Employee; NULL nếu hệ thống chốt tự động                  |
| cancelledAt           | DateTime UTC, NULL        | Thời điểm ghi hủy                                              |
| cancellationActorType | Enum[ActorType], NULL     | SYSTEM, EMPLOYEE hoặc CUSTOMER; đúng tác nhân hủy đơn/hóa đơn  |
| cancelledByCustomer   | uuid, NULL                | FK → Customer; khách tự hủy/đổi đơn chưa thu                   |
| cancellationReason    | string, NULL              | Bắt buộc khi CANCELLED                                         |
| refundedByEmployee    | uuid, NULL                | FK → Employee; người xác nhận đã hoàn tiền                     |
| refundedAt            | DateTime UTC, NULL        | Thời điểm thực hoàn                                            |
| refundMethod          | Enum[PaymentMethod], NULL | CASH hoặc BANK                                                 |
| refundAmount          | Decimal(18,0), NULL       | Nếu hoàn: bằng toàn bộ totalCost hóa đơn cũ                    |
| refundReason          | string, NULL              | Bắt buộc khi có hoàn tiền                                      |
| replacesInvoiceId     | uuid, NULL                | FK → Invoice.invoiceId, UNIQUE khi có; hóa đơn cũ bị thay thế  |
| note                  | string                    | Ghi chú lúc lập; bất biến cùng nội dung hóa đơn                |

Không có updatedAt bắt buộc riêng: các mốc nghiệp vụ và Audit lưu dấu thay đổi. Không xóa thông tin thu cũ khi hủy hóa đơn đã thanh toán. Actor hủy riêng được lưu để không gán nhầm nhân viên khi khách tự hủy hoặc hệ thống chốt; người thực hiện hoàn tiền vẫn phải là nhân viên.

**InvoiceDailyCounter** — Bộ đếm cấp mã hóa đơn theo ngày

| Field      | Type     | Ghi chú                                    |
| ---------- | -------- | ------------------------------------------ |
| issueDate  | DateOnly | PK, ngày lập theo Việt Nam                 |
| lastNumber | bigint   | Số lớn nhất đã cấp cho hóa đơn đã lưu, > 0 |

Cấp số bằng thao tác tăng nguyên tử có khóa dòng/UPSERT RETURNING trong cùng giao dịch lập hóa đơn. UNIQUE invoiceCode bảo vệ cuối; không tính số mới bằng `COUNT + 1` hoặc `MAX + 1` không có khóa. Hủy hóa đơn không giảm bộ đếm. SRS không bắt buộc số thứ tự liên tục không có khoảng trống; mã đã lập/hủy không được tái sử dụng.

### 5.3 Constraints

```sql
CREATE UNIQUE INDEX "UX_Invoice_Code" ON "Invoice" ("invoiceCode");
-- Tối đa một hóa đơn hợp lệ, bao gồm cả UNPAID và PAID.
CREATE UNIQUE INDEX "UX_Invoice_ValidPerBooking"
  ON "Invoice" ("bookingId") WHERE "status" IN ('Unpaid', 'Paid');
CREATE UNIQUE INDEX "UX_Invoice_Replaces"
  ON "Invoice" ("replacesInvoiceId") WHERE "replacesInvoiceId" IS NOT NULL;
ALTER TABLE "Invoice" ADD CONSTRAINT "CK_Invoice_Amount"
  CHECK ("totalCost" > 0
         AND ("replacesInvoiceId" IS NULL OR "replacesInvoiceId" <> "invoiceId"));
```

Các CHECK bổ sung phải kiểm tra nhóm thông tin đầy đủ, không chỉ từng trường riêng lẻ:

| Trạng thái/nhóm thông tin | Ràng buộc                                                                                                                                                                                                    |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| UNPAID                    | paidAt/paymentMethod/paidByEmployee, toàn bộ thông tin hủy và hoàn đều NULL.                                                                                                                                 |
| PAID                      | paidAt/paymentMethod/paidByEmployee đầy đủ; thông tin hủy và hoàn đều NULL.                                                                                                                                  |
| CANCELLED chưa từng thu   | Nhóm thu và hoàn đều NULL; nhóm hủy đầy đủ, đúng tác nhân.                                                                                                                                                   |
| CANCELLED đã thu          | Giữ đầy đủ nhóm thu; nhóm hủy và hoàn đầy đủ; refundAmount = totalCost, người hoàn là Employee.                                                                                                              |
| Nhóm hủy                  | cancelledAt, cancellationActorType, cancellationReason không rỗng bắt buộc khi CANCELLED, NULL ở trạng thái khác. SYSTEM không mã người; EMPLOYEE chỉ cancelledByEmployee; CUSTOMER chỉ cancelledByCustomer. |
| Nhóm hoàn                 | Hoặc cả nhóm NULL, hoặc refundedAt/refundMethod/refundAmount/refundedByEmployee/refundReason đầy đủ, reason không rỗng.                                                                                      |
| Chuỗi thay thế            | Hóa đơn cũ phải CANCELLED và cùng bookingId; mỗi hóa đơn cũ chỉ được thay một lần; không chu trình.                                                                                                          |

Phải kiểm tra các mốc thực thu/hoàn không ở tương lai theo đồng hồ xử lý; paidAt có thể trước issuedAt nếu ghi nhận sau việc đã thu, nên không đặt CHECK `paidAt >= issuedAt`. cancelledAt là thời điểm ghi hủy, không trước issuedAt. Khi có hoàn tiền, paidAt ≤ refundedAt ≤ cancelledAt. Hoàn tiền được xác nhận trước khi ghi hủy; không ghi nhận tiền đã hoàn nếu thao tác đổi/hủy chưa kiểm tra điều kiện, giá và dữ liệu hiện tại.

Chỉ mục hợp lệ bảo đảm **tối đa một**, không bảo đảm **đúng một** hay tổng hóa đơn bằng Booking: dùng constraint trigger cuối giao dịch. Hủy cũ trước rồi tạo mới trong cùng giao dịch; không cho commit một đơn đang hiệu lực thiếu hóa đơn. Tổng hóa đơn CANCELLED lịch sử không cần bằng giá Booking hiện tại sau đổi lịch.

| Enum          | Values                        |
| ------------- | ----------------------------- |
| InvoiceStatus | `UNPAID`, `PAID`, `CANCELLED` |
| PaymentMethod | `BANK`, `CASH`                |

## 6. Nhật ký, chống gửi lặp và thông báo

### 6.1 Audit — Nhật ký bất biến

Nguồn: BR-14, BR-15, UC-43, NFR-06.

| Field      | Type            | Ghi chú                                                                    |
| ---------- | --------------- | -------------------------------------------------------------------------- |
| id         | bigint          | PK                                                                         |
| actorType  | Enum[ActorType] |                                                                            |
| employeeId | uuid, NULL      | FK → Employee                                                              |
| customerId | uuid, NULL      | FK → Customer                                                              |
| action     | string          | Loại thao tác: tạo/sửa/xóa mềm/khóa/mở khóa/đổi email/quyền/thu/hoàn/chốt… |
| entityName | string          | Đối tượng                                                                  |
| entityId   | string          | Mã đối tượng; hỗ trợ UUID/int/khóa ghép                                    |
| oldValue   | JsonB, NULL     | Giá trị trước, đã loại bí mật                                              |
| newValue   | JsonB, NULL     | Giá trị sau, đã loại bí mật                                                |
| reason     | string, NULL    | Lý do khi nghiệp vụ yêu cầu                                                |
| createdAt  | DateTime UTC    |                                                                            |

CHECK tác nhân theo BookingStatusHistory. oldValue/newValue/reason/lỗi không chứa mật khẩu, mã xác minh, mã phiên hoặc hash của chúng. Audit UserAccount/VerificationCode chỉ lấy các trường được phép.

Audit bao phủ sân, lịch, giá, hồ sơ khách, hồ sơ/tài khoản nội bộ, đặt sân, hóa đơn và mọi thao tác nêu BR-14. Không tự audit bảng Audit; không cho sửa/xóa Audit và BookingStatusHistory qua ứng dụng. Dùng quyền DB/trigger phù hợp để chặn UPDATE/DELETE các bảng nhật ký bởi tài khoản ứng dụng.

INDEX `(createdAt, id)`, `(actorType, createdAt)`, `(employeeId, createdAt)`, `(customerId, createdAt)`, `(entityName, entityId)`, `(action, createdAt)`.

### 6.2 IdempotencyRequest — Kết quả chống gửi lặp

Nguồn: BR-21, UC-07–UC-10, UC-15–UC-18, UC-20–UC-22, UC-29, UC-44.

| Field          | Type         | Ghi chú                                                                                        |
| -------------- | ------------ | ---------------------------------------------------------------------------------------------- |
| id             | uuid         | PK                                                                                             |
| actorScope     | string       | Phạm vi người thao tác do máy chủ xác định                                                     |
| requestKey     | string       | Mã yêu cầu gửi lặp                                                                             |
| operation      | string       | Tạo/đổi/xác nhận/hủy/thu/hoàn/chốt…                                                            |
| requestHash    | string       | Hash nội dung nghiệp vụ chuẩn hóa, gồm loại thao tác, bản ghi và dữ liệu gốc/báo giá liên quan |
| bookingId      | uuid, NULL   | FK → Booking khi kết quả liên quan đặt sân                                                     |
| invoiceId      | uuid, NULL   | FK → Invoice khi có                                                                            |
| responseStatus | int          | Mã kết quả đã lưu                                                                              |
| responseBody   | JsonB        | Kết quả đủ trả lại, không bí mật và chỉ chứa dữ liệu người thao tác được xem                   |
| createdAt      | DateTime UTC |                                                                                                |

UNIQUE `(actorScope, requestKey)`. Loại thao tác nằm trong hash, nên dùng cùng mã cho thao tác khác bị từ chối. Lưu kết quả thành công cùng giao dịch nghiệp vụ, giữ suốt vòng đời bản ghi; không dùng TTL ngắn làm mất bảo vệ lặp. Không lưu mật khẩu/mã xác minh/mã phiên trong requestHash đầu vào hoặc responseBody.

Phạm vi đăng nhập gắn accountId; tác vụ hệ thống gắn phạm vi job do máy chủ cấp. Đặt nhanh gắn phạm vi yêu cầu ẩn danh do máy chủ cấp, kèm bằng chứng xác minh đúng email/nội dung; không cho client tự chọn email/customerId làm phạm vi được tin cậy. Xác minh đặt nhanh không cho quyền đọc hồ sơ cũ. Phạm vi gửi lại được giữ qua retry để trả kết quả cũ sau khi mã đã dùng, không dùng mã xác minh đã tiêu thụ làm căn cứ tạo đơn mới.

Sau kiểm tra quyền hiện tại: cùng mã/cùng hash trả kết quả cũ; cùng mã/khác hash từ chối. Yêu cầu cùng mã chờ giao dịch đang xử lý rồi đọc kết quả đã commit. Tác vụ chốt khóa bản ghi và kiểm tra trạng thái cuối để không tạo lặp lịch sử khi chạy bù.

### 6.3 Notification — Hàng đợi thông báo nghiệp vụ

Nguồn: BR-28, UC-07–UC-10, UC-15–UC-18, UC-20–UC-21, UC-44, NFR-09.

| Field          | Type               | Ghi chú                                                     |
| -------------- | ------------------ | ----------------------------------------------------------- |
| notificationId | uuid               | PK                                                          |
| eventKey       | string             | UNIQUE; định danh một sự kiện đã lưu và loại thông báo      |
| customerId     | uuid               | FK → Customer, người nhận                                   |
| recipientEmail | string             | Snapshot email nhận tại sự kiện                             |
| bookingId      | uuid               | FK → Booking                                                |
| eventType      | string             | Tạo đơn, đổi lịch, đổi trạng thái…                          |
| payload        | JsonB              | Nội dung sự kiện để gửi, không chứa bí mật                  |
| status         | string             | Trạng thái kỹ thuật: PENDING, PROCESSING, SENT, FAILED      |
| attemptCount   | int                | Số lần thử gửi, >= 0                                        |
| lastError      | string, NULL       | Lỗi đã loại bí mật                                          |
| nextAttemptAt  | DateTime UTC, NULL | Lịch thử lại                                                |
| lockedUntil    | DateTime UTC, NULL | Hết hạn giữ việc gửi; cho phép chạy bù khi worker gián đoạn |
| sentAt         | DateTime UTC, NULL |                                                             |
| createdAt      | DateTime UTC       |                                                             |

Notification được lưu cùng giao dịch tạo/đổi đơn; chỉ gửi email sau commit. UNIQUE eventKey chống tạo lại thông báo cùng sự kiện; retry chỉ gửi lại thông báo, không tạo lại Booking/Invoice. Worker nhận việc nguyên tử và khôi phục PROCESSING quá hạn; lỗi email giữ kết quả nghiệp vụ đã lưu. Có thể gửi email lặp nếu nhà cung cấp đã nhận nhưng worker mất kết quả; dùng khóa sự kiện chống lặp phía nhà cung cấp khi có hỗ trợ, không giả định UNIQUE trong DB bảo đảm email chỉ gửi đúng một lần.

Không đưa mã xác minh vào bảng thông báo nghiệp vụ này; việc gửi mã theo mục 2.2. AI chỉ đọc dữ liệu công khai và không tạo thông báo/đơn/hóa đơn hay đọc hồ sơ khách. SRS không yêu cầu lưu hội thoại AI nên không thêm bảng hội thoại.

| Enum      | Values                           |
| --------- | -------------------------------- |
| ActorType | `SYSTEM`, `EMPLOYEE`, `CUSTOMER` |

## 7. Toàn vẹn dữ liệu và giao dịch

### 7.1 Phân chia trách nhiệm

| Quy tắc                                                                 | Cơ chế bảo đảm                                                                                                                                                                                         |
| ----------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Duy nhất email, tên sân, mã đơn/hóa đơn, lịch/ngày, mốc giá, mã gửi lặp | UNIQUE/partial UNIQUE index ở DB; chuẩn hóa đầu vào phía máy chủ. Không mặc định email nhân viên và email khách phải duy nhất chung nếu SRS không yêu cầu; loginEmail luôn duy nhất toàn bộ tài khoản. |
| Không trùng lượt chưa hủy                                               | Exclusion constraint Booking ở DB, kiểm tra sớm trong service để trả lỗi dễ hiểu.                                                                                                                      |
| Đúng một hồ sơ cho mỗi tài khoản tồn tại, đúng loại và đồng bộ email    | Constraint trigger và khóa tài khoản/hồ sơ liên quan; mỗi UserAccount tồn tại phải có đúng một hồ sơ. Hồ sơ có accountId NULL sau hard-delete là hợp lệ, không là lỗi mồ côi.                          |
| Đúng 7 lịch/sân và bảng giá phủ giờ mở                                  | Deferred constraint trigger trên sân/lịch/giá; khóa dòng sân/lịch khi tạo hoặc thay cấu hình. Kiểm tra toàn bộ bảng giá hiện hành, không tạo phiên bản giá lịch sử.                                    |
| Đúng một hóa đơn hợp lệ và số tiền khớp đơn                             | Partial UNIQUE + deferred constraint trigger trên Booking/Invoice; khóa Booking khi thay/thu/hủy hóa đơn. Trigger kiểm tra cả khi sửa hóa đơn mà không sửa Booking.                                    |
| Chuỗi hóa đơn thay thế cùng đơn, bản cũ đã hủy và không chu trình       | FK + UNIQUE replacesInvoiceId + trigger kiểm tra liên bảng và bảo vệ nội dung bất biến.                                                                                                                |
| Snapshot giá                                                            | FK Booking + kiểm tra tập segment và tổng tiền tại cuối giao dịch; không tính lại khi xác nhận/Hoàn thành.                                                                                             |
| Lịch sử trạng thái đúng đơn và tác nhân                                 | CHECK nhóm mã tác nhân + kiểm tra CUSTOMER là chủ hồ sơ của đơn; sự kiện tạo có oldStatus NULL, các sự kiện sau phải có trạng thái cũ/mới khác nhau và đúng chuyển trạng thái SRS 3.1.                 |
| Hai yêu cầu sửa cùng bản ghi                                            | Giao dịch, khóa bản ghi, đối chiếu dữ liệu gốc mà yêu cầu dựa trên và kiểm tra lại trạng thái; nếu đã thay đổi thì từ chối/yêu cầu tải lại.                                                            |
| Trạng thái/quyền/mốc 1 giờ/sử dụng thực tế                              | Service kiểm tra lại dưới khóa giao dịch bằng đồng hồ máy chủ, theo thứ tự SRS 3.2; không cho API ghi trực tiếp trạng thái để bỏ qua quy tắc.                                                          |
| Chống gửi lặp và tác vụ chạy bù                                         | UNIQUE IdempotencyRequest + lưu cùng giao dịch, khóa và kiểm tra trạng thái; nhật ký chỉ ghi cho thay đổi thật.                                                                                        |
| Nhật ký bất biến, không xóa lịch sử                                     | Quyền DB/trigger chặn sửa/xóa; FK RESTRICT cho dữ liệu lịch sử; FK accountId SET NULL khi hard-delete tài khoản theo SRS.                                                                              |

Các constraint trigger phải được triển khai trong migration; tài liệu không coi một khóa ngoại hoặc đoạn SQL mẫu là đã thay cho trigger. Kiểm tra liên bảng cần khóa các dòng chung liên quan để tránh hai giao dịch cùng vượt kiểm tra. Mỗi enum chỉ nhận các giá trị đã liệt kê bằng enum DB hoặc CHECK thích hợp.

### 7.2 Đơn vị giao dịch

- **Xóa tài khoản:** khóa tài khoản/hồ sơ liên quan và bảo vệ QTV cuối + vô hiệu mã xác minh liên quan + hard-delete UserAccount + SET NULL accountId hồ sơ + Audit; giữ nguyên dữ liệu lịch sử và giao dịch.
- **Tạo tài khoản:** xác minh + tạo/link hồ sơ + UserAccount + tiêu thụ mã + Audit; lỗi hoàn tác toàn bộ, không để tài khoản mồ côi.
- **Đặt sân/đặt nhanh:** khóa phạm vi gửi lặp và cấu hình sân, kiểm tra quyền/khả dụng/giá, tái sử dụng hoặc tạo Customer, Booking + BookingPriceSegment + lịch sử tạo + Audit + Notification + IdempotencyRequest; đặt nhanh tiêu thụ mã trong chính giao dịch này. Lỗi lưu không tiêu thụ mã hoặc để lại hồ sơ mới không cần thiết.
- **Xác nhận/ghi nhận sử dụng:** Booking + Invoice nếu cần + lịch sử + Audit + Notification + mã gửi lặp. Giữ giá đơn; chuyển CONFIRMED → COMPLETED không lập hóa đơn mới.
- **Đổi/hủy:** Booking + snapshot giá nếu đổi + hóa đơn hủy/thay thế/thu/hoàn phù hợp + lịch sử chuyển trạng thái nếu có + Audit + Notification + mã gửi lặp. Giữ mã đơn và chủ hồ sơ.
- **Thu tiền:** khóa cả Booking/Invoice, kiểm tra đã chốt quá ngày, đối chiếu tổng tiền và thông tin thực thu, lưu Invoice + Audit + mã gửi lặp. Không tự đổi trạng thái Booking.
- **Cấu hình sân/lịch/giá:** sân + 7 lịch khi tạo, hoặc lịch/giá hiện hành bị sửa + Audit; xem trước và xác nhận cấu hình kết quả theo BR-25.
- **Đổi email/khóa/quyền:** hồ sơ và tài khoản + vô hiệu mã xác minh liên quan khi đổi email + Audit trong cùng giao dịch. Lưu các trường cho phép, không đưa bí mật vào log.

Tạo/đổi/xác nhận đơn và sửa lịch/trạng thái/xóa sân phải cùng khóa dòng BadmintonCourt liên quan; nếu chuyển sân khóa các sân theo thứ tự courtId ổn định, rồi khóa đơn và các hóa đơn liên quan theo một thứ tự thống nhất. Tất cả đường thao tác phải dùng chung thứ tự khóa để tránh kiểm tra sân còn mở xong thì bị đóng/xóa trước khi lưu, hoặc sửa lịch gây mất khả dụng cho đơn vừa tạo.

Chốt quá ngày BR-17 là **giao dịch hệ thống độc lập**, commit trước khi tiếp tục giao dịch thao tác người dùng; không hoàn tác kết quả chốt nếu yêu cầu tiếp theo bị từ chối. Tác vụ chọn mọi đơn `date < ngày Việt Nam hiện tại` còn PENDING/CONFIRMED: có hóa đơn hợp lệ PAID thì COMPLETED, còn lại CANCELLED và hủy UNPAID nếu có; ghi lịch sử SYSTEM, Audit và Notification. Trạng thái cuối giữ nguyên, kể cả COMPLETED/UNPAID. Không cần trường “ngày chốt cuối” để bỏ qua đơn bị sót: chạy bù truy vấn lại toàn bộ đơn quá ngày chưa ở trạng thái cuối.

Thu/hoàn tiền ngoài hệ thống không thể hoàn tác bằng transaction DB. Trước chuyển tiền phải kiểm tra điều kiện, giá và dữ liệu hiện tại; lỗi ghi nhận sau chuyển tiền chỉ thử lại ghi nhận bằng chứng cũ và đối chiếu trạng thái, không yêu cầu chuyển tiền lần nữa (BR-23).

## 8. Tra cứu, thống kê và đối chiếu Use Case

### 8.1 Chỉ mục phục vụ truy vấn

| Dữ liệu                       | Chỉ mục gợi ý                                                                                                                    |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Lưới sân/ngày và lọc hệ thống | Booking `(date, courtId, startTime, bookingId)`, `(status, createdAt, bookingId)`                                                |
| Lịch sử khách                 | Booking `(customerId, date DESC, startTime DESC, createdAt DESC, bookingId)`                                                     |
| Lịch sử trạng thái            | BookingStatusHistory `(bookingId, changedAt, id)`                                                                                |
| Chốt ngày chạy bù             | Partial index Booking `(date, bookingId)` WHERE status IN ('Pending', 'Confirmed')                                               |
| Tìm khách                     | Customer `(phone)`; email đã có UNIQUE; tìm họ tên kiểu chứa có thể dùng chỉ mục phù hợp khi triển khai                          |
| Hóa đơn và doanh thu          | Invoice `(status, issuedAt, invoiceId)`, `(bookingId, issuedAt, invoiceId)`, partial `(paidAt, invoiceId)` WHERE status = 'Paid' |
| Email chờ gửi lại             | Notification `(status, nextAttemptAt, notificationId)`                                                                           |

Các chỉ mục gợi ý không thay đổi chức năng hoặc ràng buộc nghiệp vụ. Danh sách phân trang mặc định 20, tối đa 100, trang từ 1; tiêu chí cuối luôn có mã bản ghi để ổn định thứ tự.

### 8.2 Dữ liệu thống kê

- **Doanh thu (UC-41):** Tổng Invoice.totalCost của hóa đơn hợp lệ PAID theo paidAt; loại mọi CANCELLED và hóa đơn mới chưa thu. Không cộng lại tiền cũ trên hóa đơn đã hoàn/hủy. Báo cáo kỳ cũ có thể thay đổi sau hoàn tiền, đây không phải báo cáo dòng tiền.
- **Lượt/giờ đặt (UC-42):** Lọc Booking.date theo ngày sử dụng; đếm/tính thời lượng tất cả trạng thái trừ CANCELLED, đồng thời tách COMPLETED. Giữ giao dịch sân đã xóa với nhãn “Sân đã xóa #<courtId>”.
- **Thời gian lọc:** Khoảng ngày gồm cả hai ngày theo Việt Nam. Khi lọc thời điểm UTC, chuyển thành `[00:00 ngày đầu, 00:00 ngày sau ngày cuối)` ở Việt Nam rồi sang UTC. Từ ngày > đến ngày bị từ chối.

Trạng thái thanh toán lấy từ hóa đơn hợp lệ, không lưu riêng trên Booking.

### 8.3 Đối chiếu chức năng và dữ liệu

| Use Case                                                              | Dữ liệu hỗ trợ                                                                                                           | Nguồn SRS                         |
| --------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ | --------------------------------- |
| UC-01–UC-04: đăng ký/xác minh/đăng nhập/quên mật khẩu                 | UserAccount, Customer, VerificationCode, Audit                                                                           | BR-11, BR-12, BR-26               |
| UC-05–UC-06: sân/lưới/giá                                             | BadmintonCourt, CourtSchedule, PricingRule, Booking; chỉ trả dữ liệu công khai                                           | BR-01–BR-03, BR-19, BR-28         |
| UC-07–UC-08, UC-20: tạo đơn/đặt nhanh/tạo thay khách                  | Customer, Booking, BookingPriceSegment, VerificationCode khi đặt nhanh, History, Audit, IdempotencyRequest, Notification | BR-04, BR-10, BR-20, BR-21, BR-26 |
| UC-09–UC-10, UC-16, UC-21: hủy/đổi/từ chối/sự cố                      | Booking, giá snapshot, Invoice/thay thế/hoàn, History, Audit, Notification                                               | BR-05, BR-13, BR-22–BR-24         |
| UC-11–UC-12: lịch sử/hồ sơ cá nhân                                    | Customer, Booking, hóa đơn hợp lệ, History; lọc theo tài khoản sở hữu                                                    | BR-18, BR-19, BR-28               |
| UC-13: AI                                                             | Chỉ dữ liệu sân/lịch/giá/khả dụng công khai và FAQ; không bổ sung bảng hội thoại                                         | BR-28, NFR-10                     |
| UC-14–UC-15, UC-17–UC-19: xử lý đơn/lập hóa đơn/ghi sử dụng/lịch ngày | Booking, snapshot giá, Invoice, InvoiceDailyCounter, History, Audit; ghi sử dụng chỉ trên đơn đã tồn tại                 | BR-06, BR-08, BR-09, BR-20        |
| UC-22, UC-28–UC-29: hoàn/xem/thu tiền                                 | Invoice, dấu vết thu/hủy/hoàn và chuỗi replacesInvoiceId, Audit, IdempotencyRequest                                      | BR-09, BR-21–BR-23                |
| UC-23, UC-31–UC-35: trạng thái/thêm/sửa/xóa/lịch/giá sân              | BadmintonCourt, CourtSchedule, PricingRule, Audit                                                                        | BR-01, BR-03, BR-16, BR-19, BR-25 |
| UC-24–UC-27, UC-30: khách/tài khoản/đổi email/khóa/xóa/tra cứu        | Customer, UserAccount, VerificationCode, Booking, Audit                                                                  | BR-10–BR-12, BR-18, BR-26, BR-27  |
| UC-36–UC-39: nhân viên/tài khoản nội bộ/quyền/xóa tài khoản           | Employee, UserAccount, VerificationCode, Audit                                                                           | BR-11, BR-15, BR-26, BR-27        |
| UC-40–UC-43: toàn hệ thống/thống kê/nhật ký                           | Booking, History, Invoice, Audit                                                                                         | QT-15–QT-21, BR-14, BR-25         |
| UC-44: chốt qua ngày                                                  | Booking, hóa đơn hợp lệ, History SYSTEM, Audit, Notification                                                             | BR-17, BR-21, NFR-09              |
