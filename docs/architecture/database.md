# Thiết kế dữ liệu — Hệ thống đặt sân cầu lông

**Phiên bản 2.1 · 30/10/2026**

Căn cứ: SRS 2.0 và Use Case 2.0. Tài liệu này mô tả cấu trúc dữ liệu; quy tắc nghiệp vụ (BR-xx) nằm trong SRS và chỉ được dẫn chiếu ở đây, không nhắc lại.

## Mục lục

1. [Quy ước chung](#1-quy-ước-chung)
2. [Tài khoản và người dùng](#2-tài-khoản-và-người-dùng)
3. [Sân, lịch và giá](#3-sân-lịch-và-giá)
4. [Đặt sân và lịch sử](#4-đặt-sân-và-lịch-sử)
5. [Hóa đơn và hoàn tiền](#5-hóa-đơn-và-hoàn-tiền)
6. [Nhật ký, chống gửi lặp và thông báo](#6-nhật-ký-chống-gửi-lặp-và-thông-báo)
7. [Toàn vẹn dữ liệu và giao dịch](#7-toàn-vẹn-dữ-liệu-và-giao-dịch)
8. [Tra cứu, thống kê và đối chiếu Use Case](#8-tra-cứu-thống-kê-và-đối-chiếu-use-case)
9. [Thay đổi tài liệu](#9-thay-đổi-tài-liệu)

## 1. Quy ước chung

| Nội dung | Quy ước |
| --- | --- |
| Khóa | UUID cho tài khoản, hồ sơ, đặt sân, hóa đơn; số nguyên cho sân, lịch, giá hiện hành. |
| Trường bắt buộc | Trường không ghi `NULL` là `NOT NULL`. FK đến dữ liệu lịch sử dùng `ON DELETE RESTRICT`, không cascade xóa giao dịch. Ngoại lệ: FK hồ sơ → `UserAccount` dùng `ON DELETE SET NULL` để xóa tài khoản mà giữ hồ sơ (BR-15). |
| Thời điểm | `DateTime UTC` → PostgreSQL `timestamptz`. Ứng dụng ghi và đọc UTC; không dùng giờ địa phương cho `createdAt`, `paidAt`, thời điểm nhật ký hoặc hết hạn mã. |
| Ngày và giờ sử dụng | `DateOnly` → `date`; `TimeOnly` → `time without time zone`, hiểu theo giờ Việt Nam. `date + startTime/endTime` là giờ địa phương của lượt sử dụng; đổi sang UTC khi so với đồng hồ máy chủ. |
| Ngày trong tuần | ISO-8601: 1 = Thứ Hai, …, 7 = Chủ Nhật. |
| Đơn vị tiền | VND. `Booking.totalCost`, `Invoice.totalCost/paidAmount/refundAmount`, `PricingRule.pricePerHour` dùng `numeric(10,2)`; `BookingPriceSegment.pricePerHour` dùng `numeric(18,0)`. Đầu vào tiền phải nguyên đồng; cộng các phần bằng decimal rồi làm tròn tổng một lần (BR-04), không để cơ sở dữ liệu làm tròn đầu vào sai. |
| Giờ mở và giờ đặt | Không qua nửa đêm; giờ mở/đóng và bắt đầu/kết thúc ở phút 00 hoặc 30, giây bằng 0. Mốc giá chính xác đến phút, không bắt buộc phút 00/30. |
| Email | Bỏ khoảng trắng đầu/cuối, chữ thường, đúng định dạng; không gộp alias, dấu chấm, phần sau dấu `+` (BR-26). Lưu giá trị chuẩn hóa để so sánh duy nhất. |
| Số điện thoại | Chuỗi, theo BR-26. Không UNIQUE, không dùng để tự gộp hồ sơ. |
| Cập nhật đồng thời | Giao dịch, khóa bản ghi, kiểm tra lại dữ liệu gốc và trạng thái (BR-21). |
| Enum | Tài liệu dùng ký hiệu viết hoa (`CONFIRMED`). EF `HasConversion<string>()` lưu tên PascalCase (`Confirmed`, `Paid`, `Refunded`). Không suy ra casing của API từ ký hiệu tài liệu. SQL minh họa dùng giá trị PascalCase của cơ sở dữ liệu. |

SQL mẫu dùng tên logic, không phải script migration; schema EF hiện hành dùng bảng `Invoices` và cột PascalCase. Ràng buộc liên bảng ở mục 7 là yêu cầu thiết kế, không khẳng định mọi trigger đã có trong migration.

## 2. Tài khoản và người dùng

Quy tắc áp dụng: BR-10, BR-11, BR-12, BR-15, BR-18, BR-26, BR-27 (SRS mục 3.5).

### 2.1 Entities

**UserAccount** — Tài khoản đăng nhập

| Field | Type | Ghi chú |
| --- | --- | --- |
| accountId | uuid | PK |
| loginEmail | string | Email chuẩn hóa, UNIQUE, không đổi |
| passwordHash | string | Chỉ lưu băm; rỗng khi `PENDING_ACTIVATION`; không ghi vào Audit |
| accountType | Enum[AccountType] | `CUSTOMER` hoặc `EMPLOYEE`; không đổi sau khi tạo |
| status | Enum[AccountStatus] | `ACTIVE`, `DISABLED` hoặc `PENDING_ACTIVATION` |
| failedLoginAttempts | int | 0–5; sai mật khẩu liên tiếp, không gộp sai mã xác minh |
| createdAt | DateTime UTC | |
| updatedAt | DateTime UTC | |

Tài khoản khách do nhân viên tạo bắt đầu ở `PENDING_ACTIVATION` với `passwordHash` rỗng và chuyển `ACTIVE` khi đặt mật khẩu thành công; trạng thái này không đăng nhập, không khóa/mở khóa, không tính `failedLoginAttempts`. Sai mật khẩu 5 lần hoặc khóa thủ công đều chuyển `DISABLED`. Mở khóa chuyển `ACTIVE` và đưa bộ đếm về 0. Không có bảng Session: mỗi yêu cầu cần đăng nhập kiểm tra tài khoản tồn tại, `ACTIVE` và quyền hiện tại phía máy chủ.

**Employee** — Hồ sơ nhân viên/QTV

| Field | Type | Ghi chú |
| --- | --- | --- |
| employeeId | uuid | PK |
| accountId | uuid, NULL | FK → `UserAccount`, UNIQUE khi có, `ON DELETE SET NULL`. Khi tạo nhân viên phải có tài khoản; `NULL` sau khi xóa tài khoản |
| fullName | string | Bắt buộc, không chỉ khoảng trắng |
| phone | string | Theo BR-26; không UNIQUE |
| email | string | Bắt buộc, chuẩn hóa, UNIQUE, đồng bộ `loginEmail`, không đổi |
| isAdmin | bool | Quyền QTV; không có hiệu lực khi hồ sơ không còn tài khoản `ACTIVE` |
| createdAt | DateTime UTC | |
| updatedAt | DateTime UTC | |

**Customer** — Hồ sơ khách hàng

| Field | Type | Ghi chú |
| --- | --- | --- |
| customerId | uuid | PK |
| accountId | uuid, NULL | FK → `UserAccount`, UNIQUE khi có, `ON DELETE SET NULL` |
| fullName | string | Bắt buộc, không chỉ khoảng trắng |
| phone | string | Theo BR-26; không UNIQUE |
| email | string | Chuẩn hóa, UNIQUE kể cả khách chưa có tài khoản; không đổi |
| createdAt | DateTime UTC | |
| updatedAt | DateTime UTC | |

Đặt nhanh bằng email đã có tài khoản vẫn dùng hồ sơ đó sau xác minh đúng mục đích, không cấp phiên hay quyền xem lịch sử (BR-10).

**VerificationCode** — Mã xác minh hiện tại theo email và mục đích

| Field | Type | Ghi chú |
| --- | --- | --- |
| email | string | PK thành phần; email chuẩn hóa |
| type | Enum[CodeType] | PK thành phần; mục đích xác minh |
| codeHash | string | Băm mã 6 chữ số |
| attempt | int | Số lần nhập sai, 0–5 |
| isUsed | bool | Đã tiêu thụ trong giao dịch thành công |
| expiresAt | DateTime UTC | Hết hạn sau 10 phút |

- Mỗi `(email, type)` giữ một mã. Gửi lại thay mã cũ, đặt `attempt = 0` và `isUsed = false`. Vô hiệu mã bằng cách đặt `expiresAt` về thời điểm hiện tại.
- Chỉ nhận mã đúng, còn hạn, chưa dùng và `attempt < 5`. Khóa bản ghi khi kiểm tra và tiêu thụ; lưu nghiệp vụ và đánh dấu `isUsed` cùng giao dịch, lỗi thì hoàn tác.
- Giới hạn gửi (BR-26) và ngữ cảnh xác minh đặt nhanh lưu tạm trong cache phía máy chủ, không thêm bảng lịch sử gửi. Bộ đếm gửi giữ đủ cửa sổ 1 giờ, độc lập với mã hiện tại. Ngữ cảnh gắn email, mục đích, mã hiện tại và nội dung đặt; đổi nội dung phải xác minh lại. Gửi lỗi không kích hoạt mã mới hay báo thành công.
- Mục đích `SET_PASSWORD`: `codeHash` là SHA-256 (Base64) của mã ngẫu nhiên 256 bit, `expiresAt` = lúc gửi + 24 giờ; nhập sai không tăng `attempt`; tiêu thụ bằng một câu lệnh cập nhật có điều kiện (`codeHash`, `isUsed = false`, `expiresAt` lớn hơn thời điểm máy chủ) trong cùng giao dịch với việc đổi mật khẩu. Giới hạn gửi lại theo email (60 giây; 5 lần/giờ) lưu trong bộ nhớ máy chủ, không thêm bảng.

### 2.2 Xóa tài khoản

QTV xóa `UserAccount` trong một giao dịch (BR-15, UC-24):

1. Kiểm tra quyền và bảo vệ QTV `ACTIVE` cuối cùng.
2. Vô hiệu mã xác minh theo `loginEmail` của tài khoản.
3. Đặt `Customer/Employee.accountId` thành `NULL` qua FK `SET NULL`; xóa bản ghi `UserAccount`.
4. Ghi nhật ký xóa với tác nhân là hồ sơ nhân viên và `entityId` là `accountId` đã xóa.

Không cascade xóa hồ sơ hay giao dịch. Nhật ký, `BookingStatusHistory` và `Invoice` tham chiếu `employeeId/customerId`, không tham chiếu bắt buộc tới tài khoản đã xóa. Yêu cầu dùng `accountId` không còn tồn tại bị từ chối, kể cả khi trình duyệt còn cookie cũ. `IdempotencyRequest.actorScope` là chuỗi định danh, không có FK tới `UserAccount`: giữ kết quả cũ để bảo toàn dấu vết nhưng không trả cho người không còn tài khoản. Không có cơ chế khôi phục tài khoản đã xóa.

### 2.3 Constraints và enums

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

- Chặn UPDATE làm đổi `UserAccount.loginEmail` hoặc `Customer/Employee.email` ở cơ sở dữ liệu; máy chủ từ chối yêu cầu đổi email (BR-18).
- CHECK: email đã trim, chữ thường, không rỗng; họ tên không chỉ khoảng trắng; số điện thoại theo BR-26.
- CHECK `failedLoginAttempts BETWEEN 0 AND 5`; bộ đếm bằng 5 buộc `status = Disabled`. Khóa thủ công không yêu cầu bộ đếm bằng 5; mở khóa đưa bộ đếm về 0.
- CHECK `attempt BETWEEN 0 AND 5`; dịch vụ chỉ nhận mã khi `attempt < 5`, `isUsed = false` và `expiresAt` > thời điểm máy chủ.
- UNIQUE trên hai bảng hồ sơ **không đủ** để bảo đảm một tài khoản chỉ thuộc một loại hồ sơ và đồng bộ email. Dùng constraint trigger cuối giao dịch (mục 7); hồ sơ sau khi xóa tài khoản được phép có `accountId NULL`.

| Enum | Giá trị |
| --- | --- |
| AccountStatus | `ACTIVE`, `DISABLED`, `PENDING_ACTIVATION` |
| AccountType | `CUSTOMER`, `EMPLOYEE` |
| CodeType | `VERIFY_EMAIL` (đăng ký), `RESET_PASSWORD`, `QUICK_BOOKING`, `SET_PASSWORD` |

`CodeType` giữ các giá trị số cũ; thêm mục đích mới ở cuối enum.

## 3. Sân, lịch và giá

Quy tắc áp dụng: BR-01, BR-03, BR-16, BR-19, BR-25 (SRS mục 3.2).

### 3.1 Entities

**BadmintonCourt**

| Field | Type | Ghi chú |
| --- | --- | --- |
| courtId | int | PK |
| name | string, NULL | `NULL` chỉ khi `DELETED`; sân chưa xóa: đã trim, bắt buộc, duy nhất không phân biệt hoa/thường |
| description | string | |
| status | Enum[CourtStatus] | |
| deletedAt | DateTime UTC, NULL | Mốc xóa; dùng xác định ngày xóa theo giờ Việt Nam khi thống kê |
| createdAt | DateTime UTC | |
| updatedAt | DateTime UTC | |

**CourtSchedule** — một lịch hiện hành cho mỗi sân và ngày trong tuần

| Field | Type | Ghi chú |
| --- | --- | --- |
| scheduleId | int | PK |
| courtId | int | FK → `BadmintonCourt` |
| dayOfWeek | int | 1–7 |
| openTime | TimeOnly | Phút 00/30, giây 0 |
| closeTime | TimeOnly | Phút 00/30, giây 0; `openTime < closeTime` |
| isAvailable | bool | `false` cho ngày nghỉ; vẫn giữ giờ và bảng giá |
| createdAt | DateTime UTC | |
| updatedAt | DateTime UTC | |

**PricingRule** — các mốc giá hiện hành

| Field | Type | Ghi chú |
| --- | --- | --- |
| pricingRuleId | int | PK |
| courtId | int | FK → `BadmintonCourt` |
| dayOfWeek | int | 1–7; FK ghép `(courtId, dayOfWeek)` → `CourtSchedule` |
| startTime | TimeOnly | Mốc bắt đầu, chính xác đến phút, giây 0 |
| pricePerHour | Decimal(10,2) | `numeric(10,2)`; đầu vào nguyên đồng VND, > 0 |
| createdAt | DateTime UTC | |
| updatedAt | DateTime UTC | |

Không thêm `endTime` vào `PricingRule` (BR-03). Khi thay toàn bộ bảng giá, khóa lịch tương ứng, kiểm tra lại lịch và giá hiện hành, thay danh sách mốc và ghi nhật ký trong cùng giao dịch. Khóa dòng lịch cũng bảo vệ cấu hình lịch và giá của ngày đó.

### 3.2 Constraints

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

- Không dùng exclusion constraint trên khoảng giờ của `CourtSchedule` vì mỗi ngày chỉ có một lịch; `tsrange` cũng không nhận hai giá trị `time`.
- Cuối giao dịch, kiểm tra: mỗi sân đủ 7 lịch; mỗi bảng giá không rỗng; mốc đầu bằng giờ mở; mọi mốc thuộc `[openTime, closeTime)`. UNIQUE chỉ bảo đảm tối đa một lịch cho mỗi ngày.

| Enum | Giá trị |
| --- | --- |
| CourtStatus | `ACTIVE`, `MAINTENANCE`, `CLOSED`, `DELETED` |

## 4. Đặt sân và lịch sử

Quy tắc áp dụng: BR-02, BR-04, BR-05, BR-06, BR-07, BR-13, BR-17, BR-20 (SRS mục 3.3).

### 4.1 Entities

**Booking**

| Field | Type | Ghi chú |
| --- | --- | --- |
| bookingId | uuid | PK |
| bookingCode | string | UNIQUE; `DS-{chữ và số}`, phần sau ít nhất 5 ký tự |
| customerId | uuid | FK → `Customer`; bắt buộc, không đổi khi đổi lịch |
| courtId | int | FK → `BadmintonCourt` |
| date | DateOnly | Ngày sử dụng theo giờ Việt Nam |
| startTime | TimeOnly | Phút 00/30, giây 0 |
| endTime | TimeOnly | Phút 00/30, giây 0; > `startTime`, tối thiểu 30 phút |
| status | Enum[BookingStatus] | |
| totalCost | Decimal(10,2) | `numeric(10,2)`; tổng nguyên VND làm tròn một lần, > 0; snapshot |
| createdAt | DateTime UTC | Thời điểm tạo thực tế, trước giờ bắt đầu |
| updatedAt | DateTime UTC | |

Trạng thái thanh toán lấy từ hóa đơn hợp lệ, không lưu trên `Booking`.

**BookingPriceSegment** — chi tiết giá đã áp dụng, snapshot theo đơn

| Field | Type | Ghi chú |
| --- | --- | --- |
| bookingId | uuid | PK thành phần, FK → `Booking` |
| segmentNo | int | PK thành phần; thứ tự phần tính tiền |
| priceStartTime | TimeOnly | Snapshot mốc giá nguồn, không FK tới bảng giá có thể bị thay |
| segmentStartTime | TimeOnly | Bắt đầu phần thời gian chịu giá |
| segmentEndTime | TimeOnly | Kết thúc phần thời gian chịu giá |
| pricePerHour | Decimal(18,0) | Snapshot đơn giá, > 0 |

- Mỗi phần lưu độc lập mốc giá và đơn giá lấy từ `PricingRule` hiện hành lúc tạo hoặc đổi lịch. Các phần phủ liên tục đúng khoảng đặt, không chồng, không thiếu.
- Tổng tiền = làm tròn một lần của tổng `số phút × pricePerHour / 60`; không làm tròn từng phần. Không cần bảng phiên bản giá để giữ giá đã áp dụng.
- Tạo đơn lưu `Booking` và các segment trong cùng giao dịch. Đổi lịch thay snapshot bằng các phần giá mới và giữ giá cũ trong nhật ký. Không tính lại khi giữ nguyên sân/ngày/giờ hoặc chỉ xác nhận/Hoàn thành.
- `segmentEndTime` là giờ kết thúc của **phần sử dụng**, không phải `endTime` của cấu hình giá.

**BookingStatusHistory**

| Field | Type | Ghi chú |
| --- | --- | --- |
| id | uuid | PK |
| bookingId | uuid | FK → `Booking` |
| oldStatus | Enum[BookingStatus], NULL | `NULL` duy nhất ở sự kiện tạo đơn |
| newStatus | Enum[BookingStatus] | |
| actorType | Enum[ActorType] | `SYSTEM`, `EMPLOYEE`, `CUSTOMER` |
| employeeId | uuid, NULL | FK → `Employee`; có khi `actorType = EMPLOYEE` |
| customerId | uuid, NULL | FK → `Customer`; có khi `actorType = CUSTOMER` |
| changedAt | DateTime UTC | Thời điểm thực ghi nhận |
| reason | string | Lý do chuyển trạng thái; chốt giờ kết thúc ghi lý do tự động rõ ràng |

- CHECK tác nhân: `SYSTEM` không có mã người; `EMPLOYEE` chỉ có `employeeId`; `CUSTOMER` chỉ có `customerId`.
- Khách xác minh đặt nhanh ghi `CUSTOMER` với hồ sơ gắn đơn, không đồng nghĩa có phiên đăng nhập. Nhân viên tạo thay khách ghi `EMPLOYEE`. Chốt giờ kết thúc ghi `SYSTEM`; không dùng tác nhân nhân viên để bỏ qua chốt.
- Không ghi `oldStatus = newStatus`. Đổi lịch giữ nguyên trạng thái được ghi vào nhật ký cùng giá trị trước/sau, không tạo chuyển trạng thái giả.

### 4.2 Constraints

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

Mốc tương lai, giờ kết thúc đã qua, giờ mở cửa, chuyển trạng thái hợp lệ và quyền tác nhân được kiểm tra dưới khóa giao dịch bằng đồng hồ máy chủ; không đưa `now()` vào CHECK vì quy tắc đổi theo thời gian. Exclusion constraint là lớp chống trùng cuối cùng, không thay việc kiểm tra khả dụng hay quyền.

| Enum | Giá trị |
| --- | --- |
| BookingStatus | `PENDING`, `CONFIRMED`, `COMPLETED`, `CANCELLED` |

## 5. Hóa đơn và hoàn tiền

Quy tắc áp dụng: BR-08, BR-09, BR-22, BR-23 (SRS mục 3.4).

### 5.1 Entities

**Invoice**

| Field | Type | Ghi chú |
| --- | --- | --- |
| invoiceId | uuid | PK |
| invoiceCode | string | UNIQUE; `HD-{yyyyMMdd}-{số thứ tự}` |
| bookingId | uuid | FK → `Booking` |
| totalCost | Decimal(10,2) | Bằng giá `Booking` lúc lập; không nhập tổng tùy ý |
| status | Enum[InvoiceStatus] | |
| issuedByEmployee | uuid, NULL | FK → `Employee`; `NULL` khi khách tự đổi lịch tạo bản thay thế, không gán giả nhân viên |
| issuedAt | DateTime UTC | Thời điểm lập thực tế |
| paidByEmployee | uuid, NULL | FK → `Employee`; người ghi nhận thu đủ |
| paidAt | DateTime UTC, NULL | Thời điểm thực thu do nhân viên xác nhận |
| paidAmount | Decimal(10,2), NULL | Nguyên dương bằng `totalCost` với `PAID`/`REFUNDED`; `NULL` với `UNPAID`/`CANCELLED` |
| paymentMethod | Enum[PaymentMethod], NULL | `CASH` hoặc `BANK` |
| cancelledByEmployee | uuid, NULL | FK → `Employee`; `NULL` nếu hệ thống chốt |
| cancelledByCustomer | uuid, NULL | FK → `Customer`; khách tự hủy/đổi đơn chưa thu |
| cancellationActorType | Enum[ActorType], NULL | Đúng tác nhân hủy đơn/hóa đơn |
| cancelledAt | DateTime UTC, NULL | Thời điểm ghi hủy |
| cancellationReason | string, NULL | Bắt buộc khi `CANCELLED`/`REFUNDED` |
| refundedByEmployee | uuid, NULL | FK → `Employee`; người xác nhận đã hoàn tiền |
| refundedAt | DateTime UTC, NULL | Thời điểm thực hoàn |
| refundMethod | Enum[PaymentMethod], NULL | `CASH` hoặc `BANK` |
| refundAmount | Decimal(10,2), NULL | Khi hoàn: nguyên dương bằng toàn bộ `totalCost` hóa đơn cũ |
| refundReason | string, NULL | Bắt buộc khi có hoàn tiền |
| replacesInvoiceId | uuid, NULL | FK → `Invoice`, UNIQUE khi có; hóa đơn cũ bị thay thế |
| note | string | Ghi chú lúc lập; bất biến cùng nội dung hóa đơn |

Không có `updatedAt` riêng: các mốc nghiệp vụ và nhật ký lưu dấu thay đổi. Chuyển `PAID` → `REFUNDED` không xóa thông tin thu cũ. Tác nhân hủy được lưu riêng để không gán nhầm nhân viên khi khách tự hủy hoặc hệ thống chốt; người thực hiện hoàn tiền luôn là nhân viên.

**InvoiceDailyCounter** — bộ đếm cấp mã hóa đơn theo ngày

| Field | Type | Ghi chú |
| --- | --- | --- |
| issueDate | DateOnly | PK; ngày lập theo giờ Việt Nam |
| lastNumber | bigint | Số lớn nhất đã cấp cho hóa đơn đã lưu, > 0 |

Cấp số bằng thao tác tăng nguyên tử (khóa dòng hoặc UPSERT RETURNING) trong cùng giao dịch lập hóa đơn. UNIQUE `invoiceCode` là lớp bảo vệ cuối. Không tính số mới bằng `COUNT + 1` hay `MAX + 1` không khóa. Hủy hóa đơn không giảm bộ đếm; mã đã lập hoặc hủy không được tái sử dụng, và số thứ tự không cần liên tục.

### 5.2 Constraints

```sql
CREATE UNIQUE INDEX "UX_Invoice_Code" ON "Invoice" ("invoiceCode");
-- Tối đa một hóa đơn hợp lệ, gồm cả UNPAID và PAID.
CREATE UNIQUE INDEX "UX_Invoice_ValidPerBooking"
  ON "Invoice" ("bookingId") WHERE "status" IN ('Unpaid', 'Paid');
CREATE UNIQUE INDEX "UX_Invoice_Replaces"
  ON "Invoice" ("replacesInvoiceId") WHERE "replacesInvoiceId" IS NOT NULL;
ALTER TABLE "Invoice" ADD CONSTRAINT "CK_Invoice_Amount"
  CHECK ("totalCost" > 0
         AND ("replacesInvoiceId" IS NULL OR "replacesInvoiceId" <> "invoiceId"));
```

Các CHECK bổ sung kiểm tra cả nhóm thông tin, không chỉ từng trường:

| Trạng thái / nhóm | Ràng buộc |
| --- | --- |
| `UNPAID` | Nhóm thu (`paidAmount`, `paidAt`, `paymentMethod`, `paidByEmployee`), nhóm hủy và nhóm hoàn đều `NULL`. |
| `PAID` | Nhóm thu đầy đủ, `paidAmount` nguyên dương bằng `totalCost`; nhóm hủy và hoàn `NULL`. |
| `CANCELLED` chưa từng thu | Nhóm thu và hoàn `NULL`; nhóm hủy đầy đủ, đúng tác nhân. |
| `REFUNDED` | Giữ đầy đủ nhóm thu (`paidAmount` = `totalCost`); nhóm hủy và hoàn đầy đủ; `refundAmount = totalCost`; người hoàn là nhân viên. |
| Nhóm hủy | `cancelledAt`, `cancellationActorType`, `cancellationReason` (không rỗng) bắt buộc khi `CANCELLED`/`REFUNDED`, `NULL` ở `UNPAID`/`PAID`. `SYSTEM` không có mã người; `EMPLOYEE` chỉ `cancelledByEmployee`; `CUSTOMER` chỉ `cancelledByCustomer`. |
| Nhóm hoàn | Chỉ `REFUNDED` có đủ `refundedAt`, `refundMethod`, `refundAmount`, `refundedByEmployee`, `refundReason` (không rỗng); trạng thái khác cả nhóm `NULL`. |
| Chuỗi thay thế | Hóa đơn tiền nhiệm phải `CANCELLED` hoặc `REFUNDED` và cùng `bookingId`; UNIQUE `replacesInvoiceId` bảo đảm mỗi tiền nhiệm có tối đa một bản kế tiếp; không tự tham chiếu, không chu trình. |

Quy tắc thời điểm:

- `paidAt`/`refundedAt` là thời điểm thực tế UTC, khác `default`, `<= serverNow`, không có dung sai lệch đồng hồ; kiểm tra ở tầng ứng dụng dưới khóa.
- `paidAt` có thể trước `issuedAt` nếu ghi nhận sau việc đã thu, nên không đặt CHECK `paidAt >= issuedAt`. `cancelledAt` không trước `issuedAt`. Khi có hoàn tiền: `paidAt ≤ refundedAt ≤ cancelledAt`.
- Hoàn tiền được xác nhận trước khi ghi hủy; không ghi nhận tiền đã hoàn nếu thao tác hủy/đổi chưa kiểm tra điều kiện, giá và dữ liệu hiện tại (BR-23).

Chỉ mục hợp lệ bảo đảm **tối đa một** hóa đơn hợp lệ cho mỗi đơn, không bảo đảm **đúng một** hay tổng bằng giá `Booking`. Quy tắc cần bảo đảm: đơn `CONFIRMED`/`COMPLETED` có đúng một hóa đơn hợp lệ; đơn `PENDING` và `CANCELLED` không có hóa đơn hợp lệ. Dùng constraint trigger cuối giao dịch: hủy hóa đơn cũ trước rồi tạo mới trong cùng giao dịch, không cho commit một đơn `CONFIRMED`/`COMPLETED` thiếu hóa đơn hợp lệ. Tổng của hóa đơn `CANCELLED`/`REFUNDED` lịch sử không cần bằng giá `Booking` hiện tại sau khi đổi lịch.

**Migration liên quan:**

- `20261027122651_AddInvoicePaidAmount`: khóa bảng `Invoices`; từ chối `TotalCost` của hóa đơn Paid/Refunded không nguyên dương hoặc không hữu hạn, dữ liệu cũ phải sửa thủ công. Backfill đã duyệt chỉ gán `PaidAmount = TotalCost` cho Paid/Refunded, giữ nguyên thời điểm, phương thức, người thu và chứng cứ khác; Unpaid/Cancelled để `NULL`. CHECK `ck_invoice_paid_amount` áp dụng cùng migration.
- `20261027080753_AddReschedulingAndInvoiceLifecycle`: cho phép người lập `NULL`, chuẩn hóa enum lưu chuỗi, thêm FK và chỉ mục duy nhất cho tiền nhiệm, thêm CHECK thu/hoàn/hủy. Dữ liệu cũ vi phạm bị từ chối, không tự dựng chứng cứ.

| Enum | Giá trị |
| --- | --- |
| InvoiceStatus | `UNPAID`, `PAID`, `CANCELLED`, `REFUNDED` |
| PaymentMethod | `BANK`, `CASH` |

## 6. Nhật ký, chống gửi lặp và thông báo

### 6.1 Audit — nhật ký bất biến

Nguồn: BR-14, BR-15, UC-40, NFR-05.

| Field | Type | Ghi chú |
| --- | --- | --- |
| id | bigint | PK |
| actorType | Enum[ActorType] | |
| employeeId | uuid, NULL | FK → `Employee` |
| customerId | uuid, NULL | FK → `Customer` |
| action | string | Loại thao tác: tạo, sửa, xóa mềm, khóa, mở khóa, quyền, thu, hoàn, chốt… |
| entityName | string | Đối tượng |
| entityId | string | Mã đối tượng; hỗ trợ UUID, int, khóa ghép |
| oldValue | JsonB, NULL | Giá trị trước, đã loại bí mật |
| newValue | JsonB, NULL | Giá trị sau, đã loại bí mật |
| createdAt | DateTime UTC | |

- CHECK tác nhân giống `BookingStatusHistory`. `oldValue`/`newValue`/lỗi không chứa mật khẩu, mã xác minh, mã phiên hay giá trị băm của chúng; với `UserAccount` và `VerificationCode` chỉ lấy các trường được phép. Thuộc tính `passwordHash` không được ghi vào Audit.
- Hệ thống tự tạo Audit trong giao dịch nghiệp vụ. `actorType/employeeId/customerId` ghi người thực hiện nghiệp vụ; việc hệ thống ghi log không đổi tác nhân thành `SYSTEM`.
- Không có trường lý do và không có API tạo hoặc sửa Audit trực tiếp. Bản ghi bất biến với mọi người dùng, kể cả nhân viên và QTV.
- Audit bao phủ sân, lịch, giá, hồ sơ khách, hồ sơ và tài khoản nội bộ, đặt sân, hóa đơn và mọi thao tác nêu ở BR-14. Không audit chính bảng Audit. Dùng quyền cơ sở dữ liệu hoặc trigger để chặn UPDATE/DELETE trên `Audit` và `BookingStatusHistory` bởi tài khoản ứng dụng.
- Chỉ mục: `(createdAt, id)`, `(actorType, createdAt)`, `(employeeId, createdAt)`, `(customerId, createdAt)`, `(entityName, entityId)`, `(action, createdAt)`.

### 6.2 IdempotencyRequest — kết quả chống gửi lặp

Nguồn: BR-21, UC-07–UC-10, UC-15, UC-18–UC-20, UC-26, UC-41.

| Field | Type | Ghi chú |
| --- | --- | --- |
| id | uuid | PK |
| actorScope | string | Phạm vi người thao tác do máy chủ xác định |
| requestKey | string | Mã chống gửi lặp |
| operation | string | Tạo, đổi, xác nhận, hủy, thu, hoàn, chốt… |
| requestHash | string | Băm nội dung nghiệp vụ chuẩn hóa: loại thao tác, bản ghi, dữ liệu gốc và báo giá liên quan |
| bookingId | uuid, NULL | FK → `Booking` khi kết quả liên quan đặt sân |
| invoiceId | uuid, NULL | FK → `Invoice` khi có |
| responseStatus | int | Mã kết quả đã lưu |
| responseBody | JsonB | Kết quả đủ để trả lại, không bí mật, chỉ chứa dữ liệu người thao tác được xem |
| createdAt | DateTime UTC | |

- UNIQUE `(actorScope, requestKey)`. Loại thao tác nằm trong hash nên dùng cùng mã cho thao tác khác bị từ chối.
- Lưu kết quả thành công cùng giao dịch nghiệp vụ và giữ suốt vòng đời bản ghi; không dùng TTL ngắn làm mất bảo vệ lặp. Không lưu mật khẩu, mã xác minh, mã phiên trong `requestHash` đầu vào hay `responseBody`.
- **Phạm vi:** người đăng nhập gắn `accountId`; tác vụ hệ thống gắn phạm vi job do máy chủ cấp; đặt nhanh gắn phạm vi yêu cầu ẩn danh do máy chủ cấp, kèm bằng chứng xác minh đúng email và nội dung. Không để client tự chọn email hoặc `customerId` làm phạm vi được tin cậy. Xác minh đặt nhanh không cho quyền đọc hồ sơ cũ. Phạm vi gửi lại được giữ qua retry để trả kết quả cũ sau khi mã đã dùng; mã xác minh đã tiêu thụ không được dùng làm căn cứ tạo đơn mới.
- **Xử lý:** sau khi kiểm tra quyền hiện tại, cùng mã và cùng hash trả kết quả cũ; cùng mã khác hash bị từ chối. Yêu cầu cùng mã chờ giao dịch đang xử lý rồi đọc kết quả đã commit. Tác vụ chốt khóa bản ghi và kiểm tra trạng thái cuối để không tạo lặp lịch sử khi chạy bù.
- **Trạng thái dự kiến (BR-21):** phản hồi `BookingDto.ExpectedState` cung cấp dấu trạng thái đơn và hóa đơn; client gửi nguyên giá trị qua `CancelBookingDto.ExpectedState`. Nội dung chống gửi lặp gồm `ExpectedState` và chứng cứ hoàn nếu có. Sau khi xử lý kết quả lặp và chốt đến hạn, kiểm tra lại dưới khóa trước hủy hoặc hoàn: thiếu hoặc khác trạng thái thì trả xung đột để tải lại; không ghi hoàn theo dữ liệu cũ, không chuyển tiền lần nữa. Không thêm cột version để thay hợp đồng này.

### 6.3 Notification — hàng đợi thông báo nghiệp vụ

Nguồn: BR-28, UC-07–UC-10, UC-15, UC-16, UC-18, UC-19, UC-41, NFR-08.

| Field | Type | Ghi chú |
| --- | --- | --- |
| notificationId | uuid | PK |
| eventKey | string | UNIQUE; định danh một sự kiện đã lưu và loại thông báo |
| customerId | uuid | FK → `Customer`, người nhận |
| recipientEmail | string | Snapshot email nhận tại sự kiện |
| bookingId | uuid | FK → `Booking` |
| eventType | string | Tạo đơn, đổi lịch, đổi trạng thái… |
| payload | JsonB | Nội dung sự kiện để gửi, không chứa bí mật |
| status | string | Trạng thái kỹ thuật: `PENDING`, `PROCESSING`, `SENT`, `FAILED` |
| attemptCount | int | Số lần thử gửi, >= 0 |
| lastError | string, NULL | Lỗi đã loại bí mật |
| nextAttemptAt | DateTime UTC, NULL | Lịch thử lại |
| lockedUntil | DateTime UTC, NULL | Hết hạn giữ việc gửi; cho phép chạy bù khi worker gián đoạn |
| sentAt | DateTime UTC, NULL | |
| createdAt | DateTime UTC | |

- Notification được lưu cùng giao dịch tạo/đổi đơn; chỉ gửi email sau commit. UNIQUE `eventKey` chống tạo lại thông báo cùng sự kiện; retry chỉ gửi lại thông báo, không tạo lại `Booking`/`Invoice`.
- Worker nhận việc nguyên tử và khôi phục `PROCESSING` quá hạn; lỗi email giữ nguyên kết quả nghiệp vụ đã lưu.
- Email có thể bị gửi lặp nếu nhà cung cấp đã nhận nhưng worker mất kết quả. Dùng khóa sự kiện chống lặp phía nhà cung cấp khi có hỗ trợ; không giả định UNIQUE trong cơ sở dữ liệu bảo đảm email chỉ gửi đúng một lần.
- Mã xác minh không đưa vào bảng này; việc gửi mã theo mục 2.1. Trợ lý AI chỉ đọc dữ liệu công khai, không tạo thông báo, đơn, hóa đơn hay đọc hồ sơ khách; SRS không yêu cầu lưu hội thoại AI nên không có bảng hội thoại.

| Enum | Giá trị |
| --- | --- |
| ActorType | `SYSTEM`, `EMPLOYEE`, `CUSTOMER` |

## 7. Toàn vẹn dữ liệu và giao dịch

### 7.1 Phân chia trách nhiệm

| Quy tắc | Cơ chế bảo đảm |
| --- | --- |
| Duy nhất email, tên sân, mã đơn/hóa đơn, lịch theo ngày, mốc giá, mã gửi lặp | UNIQUE hoặc partial UNIQUE ở cơ sở dữ liệu; chuẩn hóa đầu vào phía máy chủ. `loginEmail` duy nhất trên mọi tài khoản; email nhân viên và email khách không bắt buộc duy nhất chung. |
| Không trùng lượt chưa hủy (BR-02) | Exclusion constraint trên `Booking`; kiểm tra sớm trong service để trả lỗi dễ hiểu. |
| Mỗi tài khoản có đúng một hồ sơ, đúng loại, đồng bộ email | Constraint trigger và khóa tài khoản/hồ sơ liên quan. Hồ sơ có `accountId NULL` sau khi xóa tài khoản là hợp lệ, không phải lỗi mồ côi. |
| Đúng 7 lịch mỗi sân; bảng giá phủ giờ mở | Deferred constraint trigger trên sân/lịch/giá; khóa dòng sân và lịch khi tạo hoặc thay cấu hình. Kiểm tra toàn bộ bảng giá hiện hành, không tạo phiên bản giá lịch sử. |
| Đơn `CONFIRMED`/`COMPLETED` có đúng một hóa đơn hợp lệ (đơn `PENDING` chưa có), số tiền khớp đơn | Partial UNIQUE và deferred constraint trigger trên `Booking`/`Invoice`; khóa `Booking` khi thay, thu, hủy hóa đơn. Trigger kiểm tra cả khi sửa hóa đơn mà không sửa `Booking`. |
| Chuỗi hóa đơn thay thế | FK, UNIQUE `replacesInvoiceId` và trigger kiểm tra liên bảng, bảo vệ nội dung bất biến. |
| Snapshot giá | FK `Booking` và kiểm tra tập segment cùng tổng tiền cuối giao dịch; không tính lại khi xác nhận hoặc Hoàn thành. |
| Lịch sử trạng thái đúng đơn và tác nhân | CHECK nhóm mã tác nhân, kiểm tra khách là chủ hồ sơ của đơn; sự kiện tạo có `oldStatus NULL`, các sự kiện sau có trạng thái cũ khác mới và đúng chuyển trạng thái ở SRS mục 3.7. |
| Hai yêu cầu sửa cùng bản ghi | Giao dịch, khóa bản ghi, đối chiếu dữ liệu gốc và kiểm tra lại trạng thái (BR-21). |
| Trạng thái, quyền, mốc 1 giờ, sử dụng thực tế | Service kiểm tra lại dưới khóa giao dịch bằng đồng hồ máy chủ, theo thứ tự SRS mục 3.8; không cho API ghi trực tiếp trạng thái để bỏ qua quy tắc. |
| Chống gửi lặp và tác vụ chạy bù | UNIQUE `IdempotencyRequest`, lưu cùng giao dịch, khóa và kiểm tra trạng thái; nhật ký chỉ ghi cho thay đổi thật. |
| Nhật ký bất biến, không xóa lịch sử | Quyền cơ sở dữ liệu hoặc trigger chặn sửa/xóa; FK `RESTRICT` cho dữ liệu lịch sử; FK `accountId` dùng `SET NULL` khi xóa tài khoản. |

Constraint trigger phải được triển khai trong migration; một FK hay đoạn SQL mẫu không thay cho trigger. Kiểm tra liên bảng cần khóa các dòng chung liên quan để hai giao dịch không cùng vượt kiểm tra. Mỗi enum chỉ nhận giá trị đã liệt kê, bằng enum cơ sở dữ liệu hoặc CHECK thích hợp.

### 7.2 Đơn vị giao dịch

Mọi đơn vị dưới đây còn kèm Audit, mã chống gửi lặp (nếu có) và Notification (nếu có), trong cùng giao dịch (BR-21).

| Thao tác | Nội dung lưu trong một giao dịch |
| --- | --- |
| Xóa tài khoản (UC-24, UC-36) | Khóa tài khoản và hồ sơ liên quan, bảo vệ QTV cuối, vô hiệu mã xác minh, xóa `UserAccount`, đặt `accountId` hồ sơ thành `NULL`; giữ dữ liệu lịch sử và giao dịch. |
| Tạo tài khoản (UC-01, UC-23, UC-33) | Xác minh, tạo hoặc liên kết hồ sơ, `UserAccount`, tiêu thụ mã. Lỗi hoàn tác toàn bộ, không để tài khoản mồ côi. |
| Đặt sân, đặt nhanh, tạo thay khách (UC-07, UC-08, UC-18) | Khóa phạm vi gửi lặp và cấu hình sân; kiểm tra quyền, khả dụng, giá; dùng lại hoặc tạo `Customer`; `Booking`, `BookingPriceSegment`, lịch sử tạo. Đặt nhanh tiêu thụ mã trong chính giao dịch này; lỗi lưu không tiêu thụ mã hay để lại hồ sơ mới không cần thiết. |
| Xác nhận (UC-15) | Trước giờ bắt đầu: `Booking`, `Invoice`, lịch sử. Giữ giá đơn. Không có đường Hoàn thành thủ công. |
| Đổi hoặc hủy (UC-09, UC-10, UC-19) | `Booking`; snapshot giá nếu đổi; hóa đơn hủy, thay thế, thu hoặc hoàn tương ứng; lịch sử chuyển trạng thái nếu có. Giữ mã đơn và chủ hồ sơ. |
| Thu tiền (UC-26) | Khóa cả `Booking` và `Invoice`; chốt từ đúng giờ kết thúc trước; kiểm tra lại hạn dưới khóa; đối chiếu tổng tiền và thông tin thực thu UTC; lưu `Invoice`. Không tự đổi trạng thái `Booking`. |
| Cấu hình sân, lịch, giá (UC-28, UC-31, UC-32) | Sân và 7 lịch khi tạo, hoặc lịch và giá hiện hành bị sửa; xem trước và xác nhận kết quả theo BR-25. |
| Cập nhật hồ sơ, khóa, quyền | Hồ sơ và tài khoản; không đổi email; chỉ lưu trường được phép, không đưa bí mật vào log. |

**Thứ tự khóa.** Tạo, đổi, xác nhận đơn và sửa lịch, trạng thái, xóa sân đều khóa dòng `BadmintonCourt` liên quan. Khi chuyển sân, khóa các sân theo thứ tự `courtId` ổn định, rồi khóa đơn và các hóa đơn liên quan theo một thứ tự thống nhất. Mọi đường thao tác dùng chung thứ tự này để tránh việc kiểm tra sân còn mở xong lại bị đóng hoặc xóa trước khi lưu, hay sửa lịch làm mất khả dụng cho đơn vừa tạo.

**Chốt từ giờ kết thúc (BR-17, UC-41).** Là giao dịch hệ thống độc lập, commit trước khi tiếp tục thao tác người dùng và không bị hoàn tác nếu yêu cầu tiếp theo bị từ chối.

- Chọn mọi đơn `PENDING`/`CONFIRMED` có `date + endTime <= thời điểm máy chủ theo giờ Việt Nam`. Có hóa đơn hợp lệ `PAID` thì `COMPLETED`; còn lại `CANCELLED` và hủy `UNPAID` nếu có.
- Ghi lịch sử `SYSTEM`, Audit và Notification trong cùng giao dịch. Trạng thái cuối giữ nguyên, kể cả `COMPLETED` với hóa đơn `UNPAID` từ dữ liệu cũ.
- Worker chạy ngay khi khởi động; sau mỗi lần thành công chờ mốc :01 hoặc :31 gần nhất còn ở tương lai (giờ Việt Nam); lỗi thử lại sau 1 phút. Chạy bù mọi đơn đã đến giờ kết thúc mà chưa ở trạng thái cuối.
- Mọi nhánh sửa đơn hoặc thu tiền kiểm tra lại hạn sau khi khóa, trước khi xử lý trạng thái. Nếu vừa đến hạn, lưu chốt rồi trả xung đột để tải lại.
- Yêu cầu Hoàn thành của nhân viên (NV-05) chỉ gọi chính giao dịch chốt này: trước giờ kết thúc bị từ chối; từ giờ kết thúc kết quả do trạng thái thanh toán quyết định; đơn đã ở trạng thái cuối trả hiện trạng, không ghi lặp lịch sử.

**Thu/hoàn tiền ngoài hệ thống** không thể hoàn tác bằng giao dịch cơ sở dữ liệu. Trước khi chuyển tiền phải kiểm tra điều kiện, giá và dữ liệu hiện tại; lỗi ghi nhận sau khi chuyển tiền chỉ thử lại việc ghi bằng chứng cứ cũ và đối chiếu trạng thái, không yêu cầu chuyển tiền lần nữa (BR-23).

## 8. Tra cứu, thống kê và đối chiếu Use Case

### 8.1 Chỉ mục phục vụ truy vấn

| Dữ liệu | Chỉ mục gợi ý |
| --- | --- |
| Lưới sân theo ngày, lọc hệ thống | `Booking (date, courtId, startTime, bookingId)`, `(status, createdAt, bookingId)` |
| Lịch sử khách | `Booking (customerId, date DESC, startTime DESC, createdAt DESC, bookingId)` |
| Lịch sử trạng thái | `BookingStatusHistory (bookingId, changedAt, id)` |
| Chốt giờ kết thúc chạy bù | Partial `Booking (date, endTime, bookingId)` WHERE `status IN ('Pending', 'Confirmed')` |
| Tìm khách | `Customer (phone)`; email đã có UNIQUE; tìm họ tên kiểu chứa dùng chỉ mục phù hợp khi triển khai |
| Hóa đơn và doanh thu | `Invoice (status, issuedAt, invoiceId)`, `(bookingId, issuedAt, invoiceId)`, partial `(paidAt, invoiceId)` WHERE `status = 'Paid'` |
| Email chờ gửi lại | `Notification (status, nextAttemptAt, notificationId)` |

Chỉ mục không đổi chức năng hay ràng buộc nghiệp vụ. Danh sách phân trang mặc định 20, tối đa 100, trang từ 1; tiêu chí sắp xếp cuối luôn là mã bản ghi để ổn định thứ tự.

### 8.2 Dữ liệu thống kê

- **Doanh thu (UC-38):** tổng `Invoice.totalCost` của hóa đơn hợp lệ `PAID` theo `paidAt`; loại mọi hóa đơn `CANCELLED`/`REFUNDED` và hóa đơn mới chưa thu. Không cộng lại tiền cũ của hóa đơn đã hoàn hoặc hủy. Báo cáo kỳ cũ có thể đổi sau hoàn tiền; đây không phải báo cáo dòng tiền.
- **Lượt và giờ đặt (UC-39):** lọc `Booking.date` theo ngày sử dụng; đếm và tính thời lượng mọi trạng thái trừ `CANCELLED`, tách riêng `COMPLETED`. Giữ giao dịch của sân đã xóa với nhãn "Sân đã xóa #<courtId>".
- **Khoảng thời gian:** gồm cả hai ngày theo giờ Việt Nam. Khi lọc theo thời điểm UTC, đổi thành `[00:00 ngày đầu, 00:00 ngày sau ngày cuối)` theo giờ Việt Nam rồi sang UTC. Từ ngày > đến ngày bị từ chối.

### 8.3 Đối chiếu Use Case và dữ liệu

| Use Case | Dữ liệu hỗ trợ | Quy tắc SRS |
| --- | --- | --- |
| UC-01–UC-04: đăng ký, xác minh, đăng nhập, quên mật khẩu | `UserAccount`, `Customer`, `VerificationCode`, Audit | BR-11, BR-12, BR-26 |
| UC-05–UC-06: sân, lưới, giá | `BadmintonCourt`, `CourtSchedule`, `PricingRule`, `Booking`; chỉ trả dữ liệu công khai | BR-01, BR-03, BR-19, BR-28 |
| UC-07, UC-08, UC-18: đặt sân, đặt nhanh, tạo thay khách | `Customer`, `Booking`, `BookingPriceSegment`, `VerificationCode` (đặt nhanh), lịch sử, Audit, `IdempotencyRequest`, `Notification` | BR-04, BR-10, BR-20, BR-21, BR-26 |
| UC-09, UC-10, UC-16, UC-19: hủy, đổi, từ chối, sự cố | `Booking`, snapshot giá, `Invoice` (thay thế, hoàn), lịch sử, Audit, `Notification` | BR-05, BR-13, BR-22–BR-24 |
| UC-11, UC-12: lịch sử, hồ sơ cá nhân | `Customer`, `Booking`, hóa đơn hợp lệ, lịch sử; lọc theo tài khoản sở hữu | BR-18, BR-19, BR-28 |
| UC-13: trợ lý AI | Chỉ dữ liệu sân, lịch, giá, khả dụng công khai và FAQ | BR-28, NFR-02 |
| UC-14, UC-15, UC-17: chờ xác nhận, xác nhận (lập hóa đơn), lịch theo ngày | `Booking`, snapshot giá, `Invoice`, `InvoiceDailyCounter`, lịch sử, Audit | BR-06, BR-08, BR-09, BR-20 |
| UC-20, UC-25, UC-26: hoàn tiền, xem hóa đơn, thu tiền | `Invoice` (dấu vết thu/hủy/hoàn, chuỗi `replacesInvoiceId`), Audit, `IdempotencyRequest` | BR-09, BR-21–BR-23 |
| UC-21, UC-28–UC-32: trạng thái sân; thêm, sửa, xóa sân; giờ mở; giá | `BadmintonCourt`, `CourtSchedule`, `PricingRule`, Audit | BR-01, BR-03, BR-16, BR-19, BR-25 |
| UC-22–UC-24, UC-27, UC-42: hồ sơ khách, tạo tài khoản khách, khóa/mở khóa/xóa, tra cứu, thiết lập mật khẩu | `Customer`, `UserAccount`, `VerificationCode`, `Booking`, Audit | BR-10–BR-12, BR-15, BR-18, BR-26, BR-27 |
| UC-33–UC-36: nhân viên, tài khoản nội bộ, quyền, xóa tài khoản | `Employee`, `UserAccount`, `VerificationCode`, Audit | BR-11, BR-15, BR-26, BR-27 |
| UC-37–UC-40: đặt sân toàn hệ thống, thống kê, nhật ký | `Booking`, lịch sử, `Invoice`, Audit | BR-14, BR-17, BR-25 |
| UC-41: chốt từ giờ kết thúc | `Booking`, hóa đơn hợp lệ, lịch sử `SYSTEM`, Audit, `Notification` | BR-17, BR-21, NFR-08 |

## 9. Thay đổi tài liệu

| Phiên bản | Thay đổi |
| --- | --- |
| 1.6 | Email bất biến; Audit tự động, chỉ đọc. |
| 1.7 | Bỏ trường lý do khỏi Audit. |
| 1.8 · 28/10/2026 | Đồng bộ SRS 1.8: `PaidAmount` và backfill đã duyệt; `REFUNDED`; người lập `NULL`; hóa đơn tiền nhiệm; `ExpectedState`; thời điểm UTC nghiêm ngặt; chốt giờ kết thúc, lịch :01/:31. |
| 2.0 · 28/10/2026 | Viết lại, không đổi thiết kế: bỏ các đoạn nhắc lại quy tắc nghiệp vụ (chỉ dẫn chiếu BR trong SRS); gom chi tiết thiết kế từ SRS về đây; rút gọn mục 7; cập nhật mã UC/NFR theo Use Case 2.0 và SRS 2.0. |
| 2.1 · 30/10/2026 | Thêm `PENDING_ACTIVATION`, `SET_PASSWORD`; liên kết thiết lập mật khẩu 24 giờ; `passwordHash` không vào Audit. Không đổi cấu trúc bảng/cột. |
