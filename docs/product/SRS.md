# Tài liệu đặc tả yêu cầu phần mềm (SRS)

**Hệ thống đặt sân cầu lông** · Phiên bản 2.1 · 30/10/2026

## 1. Giới thiệu

### 1.1 Mục đích

Tài liệu đặc tả yêu cầu chức năng, quy tắc nghiệp vụ và yêu cầu phi chức năng của Hệ thống đặt sân cầu lông, làm cơ sở để thống nhất phạm vi, phát triển, kiểm thử và nghiệm thu.

### 1.2 Phạm vi

Hệ thống giúp cơ sở kinh doanh sân cầu lông quản lý sân, giờ mở cửa và bảng giá; tiếp nhận và xử lý đặt sân; lập hóa đơn; thống kê vận hành. Hệ thống phục vụ ba nhóm người dùng: khách hàng, nhân viên và quản trị viên.

Ngoài phạm vi phiên bản này:

- Cổng thanh toán trực tuyến, tự thu và tự hoàn tiền. Hệ thống chỉ ghi nhận việc thu/hoàn do nhân viên xác nhận; tiền được chuyển ngoài hệ thống. Không hỗ trợ thanh toán từng phần, giữ cọc hoặc hoàn tiền từng phần.
- Gọi điện trực tuyến (VoIP). Hotline chỉ hiển thị số điện thoại.
- Quản lý nhiều phiên đăng nhập, thiết bị và thu hồi phiên. Hệ thống vẫn kiểm tra tài khoản còn tồn tại, Hoạt động và quyền hiện tại trên mỗi yêu cầu cần đăng nhập.

### 1.3 Thuật ngữ

| Thuật ngữ | Định nghĩa |
| --- | --- |
| Khách thành viên | Khách hàng có tài khoản (hồ sơ Khách hàng liên kết với tài khoản) và đã đăng nhập. |
| Khách vãng lai | Người đặt nhanh khi chưa đăng nhập, dùng họ tên, số điện thoại và email. Hồ sơ có thể chưa có tài khoản hoặc đã liên kết tài khoản; đặt nhanh không cấp quyền vào tài khoản hay lịch sử của hồ sơ đó. |
| Nhân viên | Người dùng nội bộ có tài khoản do quản trị viên cấp, dùng hồ sơ Nhân viên. |
| Quản trị viên (QTV) | Người dùng nội bộ dùng hồ sơ Nhân viên và được cấp quyền quản trị. |
| Đặt sân | Một lượt giữ sân của một khách hàng theo ngày, giờ bắt đầu và giờ kết thúc. |
| Lưới sân (CourtGrid) | Màn hình hiển thị tình trạng từng sân theo khung giờ trong một ngày. |
| Slot | Khoảng 30 phút trên lưới sân; giờ bắt đầu và kết thúc đặt sân nằm ở phút 00 hoặc 30. |
| Mốc giá | Giờ bắt đầu áp dụng một đơn giá theo giờ; đơn giá có hiệu lực đến mốc kế tiếp hoặc giờ đóng cửa. |
| Báo giá | Tổng tiền do máy chủ tính cho lịch người dùng chọn. Báo giá chỉ để xem, không giữ chỗ. |
| Hóa đơn hợp lệ | Hóa đơn ở trạng thái Chưa thanh toán (`UNPAID`) hoặc Đã thanh toán (`PAID`). Hóa đơn Đã hủy (`CANCELLED`) và Đã hoàn tiền (`REFUNDED`) chỉ để tra cứu. |
| Xóa tài khoản | Xóa vĩnh viễn bản ghi tài khoản đăng nhập; hồ sơ, đặt sân, hóa đơn và nhật ký được giữ lại (BR-15). |
| Chờ kích hoạt (`PENDING_ACTIVATION`) | Trạng thái tài khoản khách do nhân viên tạo, chưa có mật khẩu; chuyển `ACTIVE` khi khách đặt mật khẩu qua liên kết (BR-11, BR-12, BR-26). |

### 1.4 Tài liệu tham chiếu

- Danh sách User stories (US-1 đến US-26). Cột Story ở mục 4 chỉ story nguồn của từng yêu cầu.
- Thiết kế dữ liệu của hệ thống (`database.md`).

## 2. Mô tả tổng quan

### 2.1 Nhóm người dùng

| Nhóm | Mô tả | Truy cập |
| --- | --- | --- |
| Khách vãng lai | Đặt nhanh khi chưa đăng nhập, xác minh email trước khi lưu. | Không đăng nhập |
| Khách thành viên | Đặt, hủy, đổi lịch và xem lịch sử đặt sân của mình. | Đăng ký, đăng nhập |
| Nhân viên | Xử lý đặt sân, quản lý hồ sơ và tài khoản khách hàng, hóa đơn, trạng thái sân. | Tài khoản do QTV cấp |
| Quản trị viên | Toàn bộ chức năng của nhân viên, cộng quản lý sân, giá, tài khoản, nhân sự, thống kê, nhật ký. | Tài khoản có quyền quản trị |

### 2.2 Phân quyền

| Chức năng | Khách vãng lai | Khách thành viên | Nhân viên | QTV |
| --- | :-: | :-: | :-: | :-: |
| Xem sân, lưới sân, giá | ✓ | ✓ | ✓ | ✓ |
| Trợ lý AI | ✓ | ✓ |  |  |
| Đặt sân | ✓ (đặt nhanh) | ✓ | ✓ (cho khách) | ✓ (cho khách) |
| Hủy, đổi lịch đặt sân | Qua nhân viên | Tự thực hiện khi chưa thanh toán; đã thanh toán qua nhân viên | ✓ | ✓ |
| Xem lịch sử đặt sân, hồ sơ cá nhân |  | ✓ (của mình) |  |  |
| Xử lý yêu cầu đặt sân, quản lý lịch đặt sân |  |  | ✓ | ✓ |
| Cập nhật trạng thái sân |  |  | ✓ | ✓ |
| Quản lý khách hàng, hóa đơn, tra cứu nhanh |  |  | ✓ | ✓ |
| Quản lý sân, lịch mở cửa, bảng giá |  |  |  | ✓ |
| Quản lý tài khoản khách hàng (tạo, cập nhật, khóa, mở khóa) |  |  | ✓ | ✓ |
| Quản lý tài khoản nội bộ, phân quyền, nhân viên |  |  |  | ✓ |
| Theo dõi toàn hệ thống, thống kê, nhật ký |  |  |  | ✓ |

Ô trống nghĩa là không được cấp quyền. QTV kế thừa quyền nhân viên. Quyền của khách thành viên chỉ áp dụng cho hồ sơ và đặt sân liên kết với tài khoản đang đăng nhập; biết mã đặt sân hoặc email không tạo ra quyền truy cập.

### 2.3 Môi trường và ràng buộc

- Hệ thống là ứng dụng web, truy cập qua trình duyệt. Đơn vị tiền tệ: VND.
- Ngày trong tuần theo ISO-8601: 1 = Thứ Hai, ..., 7 = Chủ Nhật.
- Ngày/giờ sử dụng sân và mốc chốt giờ kết thúc theo giờ Việt Nam (UTC+07:00); thời điểm lưu trữ theo UTC (NFR-06).
- Bộ lọc từ ngày/đến ngày gồm cả hai ngày theo giờ Việt Nam. Từ ngày > đến ngày bị từ chối.
- Danh sách quản trị, lịch sử đặt sân và nhật ký có phân trang: mặc định 20 bản ghi, tối đa 100 mỗi trang, trang bắt đầu từ 1; thứ tự hiển thị ổn định theo mã bản ghi khi trùng tiêu chí sắp xếp.

## 3. Quy tắc nghiệp vụ

Mỗi quy tắc có mã BR-xx và được viết thành các điều khoản ngắn. Quy tắc xử lý chung (BR-21) áp dụng cho mọi thao tác thay đổi dữ liệu và chỉ được định nghĩa một lần; các mục khác chỉ dẫn chiếu đến nó.

| Nhóm | Quy tắc |
| --- | --- |
| 3.1 Xử lý chung | BR-21 |
| 3.2 Sân, giờ mở cửa và giá | BR-01, BR-03, BR-04, BR-16, BR-19, BR-25 |
| 3.3 Đặt sân | BR-02, BR-05, BR-06, BR-07, BR-17, BR-20, BR-22, BR-24 |
| 3.4 Hóa đơn và hoàn tiền | BR-08, BR-09, BR-23 |
| 3.5 Khách hàng và tài khoản | BR-10, BR-11, BR-12, BR-15, BR-18, BR-26, BR-27 |
| 3.6 Lịch sử, nhật ký, thông báo và hiển thị | BR-13, BR-14, BR-28 |

### 3.1 Xử lý chung

#### BR-21 Đồng thời, nguyên tử và gửi lại

- **Nguyên tử:** một thao tác nghiệp vụ lưu mọi thay đổi liên quan (hồ sơ/tài khoản, đặt sân, hóa đơn, lịch sử, nhật ký) trong một giao dịch. Lỗi ở bất kỳ bước nào thì hoàn tác cả thao tác. Ngoại lệ: chốt độc lập theo BR-17 đã lưu thì không bị hoàn tác.
- **Xung đột:** quyền, trạng thái, thời gian, giá và ràng buộc duy nhất được kiểm tra lại ngay khi thực thi, dưới cơ chế chống xung đột. Hai yêu cầu cùng đặt một chỗ thì chỉ một thành công. Yêu cầu dựa trên dữ liệu đã thay đổi bị từ chối và người dùng phải tải lại.
- **Gửi lặp:** mỗi yêu cầu tạo, đổi, xác nhận, hủy, thu tiền mang một mã chống gửi lặp gắn với người thao tác. Cùng mã và cùng nội dung: trả kết quả cũ, không ghi thêm, không chuyển tiền lại. Cùng mã nhưng khác nội dung: từ chối. Mã được lưu cùng giao dịch và giữ suốt vòng đời bản ghi. Tác vụ tự xử lý cũng không tạo hóa đơn hoặc lịch sử lặp.
- **Thời điểm:** dùng thời điểm máy chủ sau khi giành quyền xử lý bản ghi; không dùng đồng hồ trình duyệt hay thời điểm nhấn nút.
- **Trạng thái dự kiến:** yêu cầu hủy hoặc hoàn tiền mang theo trạng thái đơn và hóa đơn mà người dùng đã thấy. Sau khi kiểm tra quyền và kết quả chống gửi lặp, hệ thống chốt đơn đến hạn (BR-17) rồi đối chiếu đơn và hóa đơn. Thiếu hoặc khác trạng thái: trả xung đột để tải lại; không ghi hoàn tiền dựa trên dữ liệu cũ.

### 3.2 Sân, giờ mở cửa và giá

#### BR-01 Giờ mở cửa

- Mỗi sân có đúng một lịch cho mỗi ngày trong tuần, duy nhất theo cặp sân và ngày trong tuần. Lịch gồm giờ mở, giờ đóng và trạng thái khả dụng.
- Giờ mở < giờ đóng, cùng ở phút 00 hoặc 30. Mỗi ngày chỉ có một khoảng mở cửa; không hỗ trợ lịch hoặc đặt sân qua nửa đêm.
- Không xóa riêng lịch của một ngày; ngày nghỉ được đánh dấu không khả dụng.
- Đặt sân phải nằm trọn trong giờ mở cửa của ngày đó và lịch phải khả dụng.

#### BR-03 Bảng giá theo mốc giờ

- Giá cấu hình theo sân và ngày trong tuần, là danh sách mốc giờ tăng dần, không trùng. Đơn giá mỗi mốc là số nguyên VND > 0.
- Mốc đầu tiên bằng giờ mở cửa; mọi mốc nằm từ giờ mở cửa đến trước giờ đóng cửa.
- Đơn giá của một mốc áp dụng đến mốc kế tiếp; mốc cuối áp dụng đến giờ đóng cửa. Bảng giá không lưu giờ kết thúc riêng và luôn phủ trọn giờ mở cửa.

#### BR-04 Tính tiền và lưu giá

- Máy chủ tính tổng theo số phút thực tế chịu từng đơn giá (BR-03), cộng các phần rồi làm tròn một lần đến đồng gần nhất (từ 0,5 đồng làm tròn lên).
- Tổng tiền và các mốc giá đã dùng được lưu khi tạo đơn. Chỉ tính lại khi đổi sang sân, ngày hoặc giờ khác; không tính lại khi xác nhận hoặc Hoàn thành. Đổi bảng giá về sau không đổi giá đã lưu.
- Báo giá không giữ chỗ. Nếu giá hoặc tổng tiền đổi trước khi lưu, hệ thống trả báo giá mới để người thao tác xác nhận lại, không âm thầm lưu số tiền khác.
- Ví dụ: 16:30–18:00, giá trước 17:00 là 60.000 đ/giờ, sau 17:00 là 100.000 đ/giờ → 30 × 60.000/60 + 60 × 100.000/60 = 130.000 đ.

#### BR-16 Bảo vệ lịch và trạng thái sân

- Từ chối đổi lịch mở cửa nếu có đơn Chờ xác nhận/Đã xác nhận của ngày bị ảnh hưởng sẽ nằm ngoài giờ mở mới hoặc rơi vào ngày không khả dụng. Mở rộng giờ, hoặc thay đổi không ảnh hưởng các đơn này, được phép.
- Chuyển sân sang Bảo trì/Đóng bị từ chối khi còn đơn Chờ xác nhận/Đã xác nhận gắn với sân. Xử lý các đơn đó theo BR-24.
- Sửa lịch có hiệu lực từ ngày hiện tại, không sửa lịch của ngày đã qua. Thay đổi trong ngày hiện tại không được loại khỏi giờ mở các lượt Hoàn thành đã ghi nhận trong ngày.
- Việc kiểm tra và thay đổi được bảo vệ khỏi tạo/xác nhận/đổi đơn đồng thời (BR-21).

#### BR-19 Xóa mềm sân

- QTV chuyển sân sang Đã xóa (`DELETED`) và đặt tên thành `NULL` trong cùng giao dịch (không dùng chuỗi rỗng). Tên cũ được lưu trong nhật ký; sân mới được dùng lại tên đó.
- Tên sân: bỏ khoảng trắng đầu/cuối, bắt buộc, không trùng khi so sánh không phân biệt hoa/thường. Chỉ sân Đã xóa có tên `NULL`.
- Giữ mã sân, lịch, giá và giao dịch cũ; màn hình lịch sử dùng nhãn "Sân đã xóa #<mã sân>".
- Sân Đã xóa không hiển thị trên lưới và danh sách đặt mới; không được sửa hoặc khôi phục.
- Phải xử lý hết đơn Chờ xác nhận/Đã xác nhận trước khi xóa. Kiểm tra và xóa được bảo vệ khỏi đồng thời (BR-21).
- Xóa lặp lại trả kết quả Đã xóa, không ghi thêm nhật ký.

#### BR-25 Cấu hình giờ mở cửa và bảng giá

- **Tạo sân:** QTV nhập giờ mở/đóng mặc định và đơn giá mặc định > 0. Hệ thống tạo 7 lịch cùng cấu hình và một mốc giá bằng giờ mở cho mỗi ngày; sau đó QTV sửa từng ngày.
- Ngày không khả dụng vẫn giữ lịch và bảng giá hợp lệ.
- **Đổi giờ mở/đóng:** bỏ các mốc ngoài khoảng mới; thêm mốc tại giờ mở mới bằng đơn giá đang có hiệu lực tại đó (hoặc đơn giá của mốc đầu cũ nếu mở sớm hơn); giữ các mốc nằm giữa giờ mở mới và giờ đóng mới; mốc cuối kéo dài đến giờ đóng.
- QTV xem trước và xác nhận kết quả. Lịch và giá lưu cùng giao dịch, có nhật ký, không để sân thiếu giá.
- Bảng giá QTV nhập phải hợp lệ toàn bộ; hệ thống không tự sửa dữ liệu giá không hợp lệ và không chấp nhận bảng rỗng.
- Giá trên đơn đã tạo được bảo toàn (BR-04); không cần lưu phiên bản bảng giá để tính lại giao dịch quá khứ.

### 3.3 Đặt sân

#### BR-02 Không trùng lịch

- Một sân có tối đa một đặt sân chưa hủy tại cùng thời điểm.
- Khoảng sử dụng tính từ giờ bắt đầu (gồm) đến giờ kết thúc (không gồm): 08:00–09:00 và 09:00–10:00 không trùng.
- Kiểm tra khi tạo, đổi lịch, xác nhận và chuyển Hoàn thành; khi đổi lịch, loại chính đơn đang đổi khỏi phép kiểm tra.
- Được bảo đảm ở cơ sở dữ liệu và trong cùng giao dịch (BR-21).

#### BR-20 Ngày giờ đặt sân

- Giờ chính xác đến phút; bắt đầu và kết thúc ở phút 00 hoặc 30; bắt đầu < kết thúc, cùng một ngày; thời lượng tối thiểu 30 phút.
- Mọi đơn mới, kể cả nhân viên tạo thay khách, phải có giờ bắt đầu > thời điểm máy chủ và khởi tạo ở Chờ xác nhận. Không tạo đơn cho lượt đã bắt đầu hoặc đã kết thúc, và không tạo trực tiếp ở Hoàn thành.
- Xác nhận chỉ được thực hiện trước giờ bắt đầu, khi sân Hoạt động và lịch khả dụng.
- Không hồi sinh đơn Đã hủy.

#### BR-07 Mã đặt sân

Mã tự sinh theo định dạng DS-{chữ và số}, phần chữ và số có ít nhất 5 ký tự, không trùng nhau. Ví dụ: DS-12AB5.

#### BR-06 Vòng đời đặt sân

- **Chờ xác nhận** (`PENDING`): yêu cầu chưa được nhân viên xác nhận.
- **Đã xác nhận** (`CONFIRMED`): nhân viên đã xác nhận, không phụ thuộc đã thanh toán hay chưa.
- **Hoàn thành** (`COMPLETED`): trạng thái cuối do chốt giờ kết thúc (BR-17); không chứng minh khách có mặt.
- **Đã hủy** (`CANCELLED`): đặt sân bị hủy.
- Chuyển trạng thái: Chờ xác nhận → Đã xác nhận (trước giờ bắt đầu); Chờ xác nhận hoặc Đã xác nhận → Hoàn thành hoặc Đã hủy (chốt BR-17, hoặc hủy trước hạn theo quy tắc tương ứng).
- Hoàn thành và Đã hủy là trạng thái cuối. Không có Hoàn thành thủ công.

#### BR-05 Hủy và đổi lịch theo yêu cầu khách

- Chỉ áp dụng cho đơn Chờ xác nhận hoặc Đã xác nhận, khi thời điểm máy chủ ≤ giờ bắt đầu hiện tại − 1 giờ (đúng mốc 1 giờ vẫn được xử lý).
- Khi đổi lịch, giờ bắt đầu mới cũng phải cách thời điểm xử lý ít nhất 1 giờ và thỏa BR-01, BR-02, BR-20.
- Đơn chưa thanh toán: khách thành viên tự xử lý, khách vãng lai qua nhân viên. Đơn đã thanh toán: chỉ nhân viên xử lý (BR-23).
- Áp dụng cả khi nhân viên làm thay khách. Hủy vì sự cố vận hành theo BR-24.

#### BR-22 Đổi lịch và hủy đơn chưa thanh toán

- Đổi lịch có hiệu lực ngay, không chờ duyệt; giữ mã đơn, chủ hồ sơ và trạng thái.
- Khách thành viên tự đổi đơn chưa thanh toán của mình. Khách vãng lai và đơn đã thanh toán đi qua nhân viên/QTV. Điều kiện về giờ theo BR-05.
- Chọn đúng sân, ngày, giờ hiện tại: trả kết quả không đổi, không tính lại giá, không thay hóa đơn, không hoàn tiền, không ghi thêm thay đổi.
- Chọn lịch khác: máy chủ trả báo giá; người thao tác xác nhận tổng tiền và trạng thái dự kiến (BR-21). Không nhập tổng tiền khác báo giá.
- Hóa đơn khi đổi lịch: đơn Chờ xác nhận chưa có hóa đơn. Đơn Đã xác nhận chưa thu: hóa đơn `UNPAID` cũ chuyển `CANCELLED`, hệ thống lập bản thay thế `UNPAID` theo tổng tiền mới, liên kết hóa đơn tiền nhiệm và lưu lý do, tác nhân, thời điểm.
- Hủy đơn chưa thu đồng thời hủy hóa đơn `UNPAID` nếu có.
- Không sửa thông tin thu cũ hoặc giá của lượt Hoàn thành. Lỗi thì giữ nguyên dữ liệu (BR-21).

#### BR-24 Hủy do sự cố vận hành

- Nhân viên/QTV được hủy đơn Chờ xác nhận/Đã xác nhận khi sân không thể phục vụ, kể cả sau mốc 1 giờ. Phải nêu lý do sự cố và xác nhận khách chưa sử dụng lượt sân.
- Khách đã sử dụng lượt sân thì không được hủy để xóa lượt sử dụng; vẫn theo chốt BR-17.
- Hóa đơn xử lý theo BR-22/BR-23; hệ thống thông báo cho khách. Không đổi đơn đã ở trạng thái cuối.
- Muốn chuyển sân sang Bảo trì/Đóng/Đã xóa, phải hoàn tất việc hủy hoặc đổi các đơn còn hiệu lực trước; hệ thống không tự bỏ qua hay tự xóa chúng.

#### BR-17 Tự chốt từ giờ kết thúc

- Khi thời điểm máy chủ ≥ giờ kết thúc lượt sân (giờ Việt Nam), đơn còn Chờ xác nhận/Đã xác nhận được chốt: có hóa đơn hợp lệ Đã thanh toán thì chuyển Hoàn thành; ngược lại chuyển Đã hủy và hủy hóa đơn Chưa thanh toán nếu có.
- Không chốt tại giờ bắt đầu hoặc nửa đêm. Chốt tự động không là bằng chứng khách có mặt.
- Trước giờ kết thúc, yêu cầu Hoàn thành bị từ chối; từ giờ kết thúc, hệ thống chốt theo quy tắc trên. Không có ngoại lệ thủ công để cứu đơn chưa thu.
- Đơn Hoàn thành hoặc Đã hủy giữ nguyên, kể cả Hoàn thành chưa thu tiền (vẫn thu được sau, BR-09).
- Tác vụ chạy ngay khi khởi động; sau mỗi lần thành công, chờ mốc :01 hoặc :31 gần nhất còn ở tương lai (giờ Việt Nam). Lỗi thì thử lại sau 1 phút và xử lý bù mọi đơn đã đến hạn.
- Trước mọi thao tác thay đổi đơn, hệ thống áp dụng cùng quy tắc chốt trong một giao dịch độc lập đã lưu, kể cả khi yêu cầu tiếp theo bị từ chối. Sau khi khóa bản ghi, kiểm tra lại thời điểm và trạng thái để xử lý cạnh tranh với thu tiền, xác nhận, đổi, hủy; không cứu đơn đã đến hạn phải hủy.
- Mỗi lần chốt lưu trạng thái, hủy hóa đơn nếu cần, lịch sử và nhật ký (tác nhân hệ thống) trong cùng giao dịch. Chạy lặp không ghi thêm.

### 3.4 Hóa đơn và hoàn tiền

#### BR-08 Lập hóa đơn tự động

- Đơn Chờ xác nhận chưa có hóa đơn.
- Khi nhân viên xác nhận, đơn chuyển Đã xác nhận và hệ thống tạo đúng một hóa đơn trong cùng giao dịch: Chưa thanh toán nếu chưa thu tiền; Đã thanh toán nếu nhân viên/QTV ghi nhận thu ngay (theo BR-09), kèm người xác nhận.
- Chốt Hoàn thành chỉ giữ hóa đơn Đã thanh toán hiện có; không tạo hóa đơn hay ghi nhận thu mới tại hoặc sau giờ kết thúc.
- Tổng tiền hóa đơn hợp lệ bằng tổng tiền đặt sân. Thay đổi theo BR-22, BR-23.

#### BR-09 Hóa đơn

- **Mã:** HD-{yyyyMMdd}-{số thứ tự}; ngày lập theo giờ Việt Nam; số thứ tự tăng trong ngày, không tái sử dụng, duy nhất kể cả khi lập đồng thời.
- **Số lượng:** mỗi đơn Đã xác nhận hoặc Hoàn thành có đúng một hóa đơn hợp lệ.
- **Trạng thái:** `UNPAID` → `PAID` khi ghi nhận thu; `UNPAID` → `CANCELLED` khi hủy đơn hoặc lập bản thay thế; `PAID` → `REFUNDED` theo BR-23. `CANCELLED` và `REFUNDED` chỉ tra cứu, không kích hoạt lại, không nhận thanh toán.
- **Ghi nhận thu:** nhân viên/QTV nhập số tiền thực thu (số nguyên VND > 0, bằng tổng tiền hóa đơn), phương thức (Tiền mặt hoặc Chuyển khoản) và thời điểm thực thu; hệ thống lưu người xác nhận. Thu tiền không tự chuyển đơn sang Hoàn thành.
- **Thời điểm thực thu/hoàn:** lưu theo UTC, phải là thời điểm thực tế, không lớn hơn thời điểm máy chủ, không có dung sai lệch đồng hồ. Thời điểm hoàn ≥ thời điểm thu.
- **Hạn thu:** được thu trong lúc sử dụng nhưng phải trước giờ kết thúc (08:00–09:00 thu lúc 08:10 là hợp lệ); từ đúng giờ kết thúc áp dụng BR-17 trước. Đơn Hoàn thành chưa thu vẫn được thu sau.
- **Bất biến:** nội dung hóa đơn không đổi. Số tiền thực thu bắt buộc, bằng tổng tiền, với `PAID`/`REFUNDED`; để trống với `UNPAID`/`CANCELLED`. Hoàn tiền giữ nguyên chứng cứ thu.
- Lưu chứng cứ thu/hoàn/hủy gồm lý do, tác nhân, thời điểm; bản thay thế liên kết với hóa đơn tiền nhiệm.
- Không hủy riêng hóa đơn làm đơn Đã xác nhận hoặc Hoàn thành thiếu hóa đơn hợp lệ.
- Nhân viên lập có thể trống khi khách tự đổi lịch tạo bản thay thế; tác nhân được ghi đúng là khách, không gán nhân viên giả.

#### BR-23 Đơn đã thanh toán và hoàn tiền

- Chỉ nhân viên/QTV hủy hoặc đổi đơn đã thanh toán; yêu cầu của khách vẫn phải thỏa BR-05.
- **Đổi lịch cùng tổng tiền:** giữ hóa đơn `PAID`, không hoàn hay thu lại.
- **Hủy, hoặc đổi sang tổng tiền khác:** hoàn toàn bộ tiền ngoài hệ thống. Nhân viên nhập số tiền hoàn (bằng tổng hóa đơn cũ, số nguyên VND > 0), phương thức, thời điểm thực hoàn (theo BR-09) và lý do; hệ thống lưu người xác nhận. Không hoàn một phần.
- Hóa đơn cũ chuyển `REFUNDED`, giữ chứng cứ thu cũ và bổ sung chứng cứ hoàn.
- Hủy đơn không lập bản thay thế. Đổi lịch lập hóa đơn mới luôn `UNPAID`, liên kết tiền nhiệm; không tự ghi `PAID` và không chuyển khoản thu cũ sang bản mới. Thu khoản mới là thao tác riêng (BR-09).
- Trước khi chuyển tiền ngoài hệ thống, kiểm tra điều kiện, báo giá và trạng thái dự kiến (BR-21). Việc ghi nhận nằm cùng giao dịch hủy/đổi.
- Lỗi lưu sau khi đã chuyển tiền: thử lại ghi nhận bằng chứng cứ cũ và đối chiếu trạng thái; nếu xung đột thì chuyển nhân viên xử lý, không chuyển tiền lần nữa.

### 3.5 Khách hàng và tài khoản

#### BR-10 Khách hàng và đặt nhanh

- Mỗi đặt sân thuộc đúng một hồ sơ Khách hàng. Mỗi email chuẩn hóa chỉ có một hồ sơ.
- Đặt nhanh bằng email đã có thì dùng lại hồ sơ đó; email mới thì tạo hồ sơ chưa liên kết tài khoản. Không cần tạo tài khoản.
- Trước khi lưu, người đặt xác minh quyền dùng email bằng mã (BR-26), kể cả email đã có tài khoản.
- Đặt nhanh không ghi đè hồ sơ cũ và không trả tên, điện thoại, mã hồ sơ hay lịch sử cũ.
- Tài khoản nhân viên/QTV không sở hữu đặt sân. Họ tạo thay khách sau khi kiểm tra thông tin liên hệ và dùng lại hồ sơ nếu email đã có.

#### BR-11 Tài khoản và tái sử dụng hồ sơ

- Mỗi tài khoản liên kết đúng một hồ sơ (Nhân viên hoặc Khách hàng, không đồng thời cả hai). Mỗi hồ sơ liên kết tối đa một tài khoản.
- Tài khoản nội bộ chỉ do QTV tạo. Tài khoản khách do khách tự đăng ký, hoặc do nhân viên tạo sau khi xác minh thông tin khách.
- Tài khoản khách do nhân viên tạo có trạng thái `PENDING_ACTIVATION` và chưa có mật khẩu. Hệ thống gửi đến email hồ sơ liên kết thiết lập mật khẩu hiệu lực 24 giờ (BR-26). Liên kết hết hạn không làm mất hồ sơ khách và lịch sử đặt sân; khách tự yêu cầu gửi lại liên kết mới, không cần nhân viên tạo lại.
- Email đăng ký đã có hồ sơ chưa liên kết: dùng lại mã khách hàng và lịch sử; không tạo hồ sơ trùng, không ghi đè thông tin cũ bằng dữ liệu từ form.
- Khách tự đăng ký phải xác minh email trước khi liên kết. Nếu email đã dùng để đăng nhập hoặc hồ sơ đã có tài khoản, từ chối tài khoản thứ hai và hướng dẫn đăng nhập hoặc quên mật khẩu.
- Tạo đồng thời phải bảo đảm duy nhất; yêu cầu thua xung đột không để lại tài khoản hoặc hồ sơ mồ côi.

#### BR-12 Khóa đăng nhập

- Tài khoản có `ACTIVE`, `DISABLED` hoặc `PENDING_ACTIVATION`; "khóa" và "vô hiệu hóa" là cùng một trạng thái `DISABLED`.
- `PENDING_ACTIVATION` không đăng nhập được, không thuộc phạm vi khóa/mở khóa, không tính bộ đếm sai mật khẩu và không dùng được "Quên mật khẩu". Khách đặt mật khẩu thành công thì chuyển `ACTIVE` (BR-26).
- Sai mật khẩu 5 lần liên tiếp: chuyển `DISABLED` ngay ở lần thứ 5 và ghi nhật ký. Bộ đếm chỉ tính sai mật khẩu của tài khoản `ACTIVE`, tách khỏi bộ đếm sai mã xác minh.
- Không tự mở theo thời gian hoặc khi đặt lại mật khẩu. Mở khóa (`DISABLED` → `ACTIVE`) đưa bộ đếm về 0; đăng nhập thành công cũng đưa bộ đếm về 0.
- Nhân viên/QTV mở khóa tài khoản khách; chỉ QTV mở khóa tài khoản nội bộ.
- Trên mỗi yêu cầu cần đăng nhập, máy chủ kiểm tra tài khoản còn tồn tại, `ACTIVE` và quyền hiện tại. Tài khoản `DISABLED` hoặc đã xóa không dùng được chức năng cần đăng nhập.
- Khách bị khóa vẫn đặt nhanh được sau xác minh email và làm việc qua nhân viên. Đặt nhanh không cấp quyền vào tài khoản hay lịch sử.

#### BR-15 Xóa tài khoản và bảo toàn lịch sử

- Xóa tài khoản là xóa vĩnh viễn bản ghi tài khoản đăng nhập; chỉ QTV thực hiện (BR-27).
- Không xóa hồ sơ Nhân viên/Khách hàng, đặt sân, hóa đơn hay nhật ký. Trong cùng giao dịch, hồ sơ trở thành không còn liên kết tài khoản; mã khách/nhân viên và dữ liệu giao dịch giữ nguyên.
- Xóa tài khoản không hủy đặt sân hay hóa đơn, không tương đương khóa, và không có thao tác khôi phục tài khoản đã xóa.
- Không có chức năng xóa hồ sơ khách đã giao dịch. Đặt sân và hóa đơn được hủy bằng trạng thái; sân dùng xóa mềm (BR-19).
- Thay lịch và bảng giá hiện hành (BR-03, BR-25) không làm mất nhật ký hay giá đã lưu của giao dịch.

#### BR-18 Email bất biến

- Email hồ sơ Khách hàng/Nhân viên và email đăng nhập không đổi sau khi tạo, kể cả bởi nhân viên hoặc QTV. Máy chủ từ chối yêu cầu làm thay đổi email.
- Chỉ nhập email khi tạo mới. Liên kết tài khoản với hồ sơ có sẵn phải dùng đúng email của hồ sơ.

#### BR-26 Email, mật khẩu và mã xác minh

- **Email:** bỏ khoảng trắng đầu/cuối, chuẩn hóa chữ thường khi so sánh duy nhất, đúng định dạng. Không gộp alias, không bỏ dấu chấm hay phần sau dấu `+`.
- **Mật khẩu:** 8–128 ký tự; không cắt ngắn, không tự bỏ khoảng trắng.
- **Thông tin liên hệ:** họ tên, số điện thoại, email bắt buộc khi tạo hồ sơ hoặc đặt nhanh. Họ tên không chỉ có khoảng trắng. Số điện thoại, sau khi bỏ dấu cách và dấu gạch ngang, được có dấu `+` ở đầu và gồm 8–15 chữ số; lưu dạng chuỗi và không dùng để gộp hồ sơ.
- **Mã xác minh:** 6 chữ số, hết hạn sau 10 phút, dùng một lần, tối đa 5 lần sai (lần sai thứ 5 vô hiệu mã). Bộ đếm sai mã tách khỏi bộ đếm đăng nhập.
- **Phạm vi mã:** gắn với email và mục đích (đăng ký, đặt lại mật khẩu, đặt nhanh), không dùng chéo. Mã đặt nhanh còn gắn với nội dung yêu cầu đặt.
- **Gửi lại:** sớm nhất sau 60 giây và vô hiệu mã cũ cùng mục đích. Trong cửa sổ 1 giờ tính lùi từ thời điểm yêu cầu: tối đa 5 lần gửi cho mỗi email và mục đích; tối đa 30 yêu cầu gửi mã cho mỗi nguồn truy cập. Vượt giới hạn: trả yêu cầu thử lại sau, không khóa tài khoản đăng nhập.
- Đặt nhanh chỉ lưu đơn và tiêu thụ mã trong cùng giao dịch; lỗi lưu thì không tiêu thụ mã. Đặt lại mật khẩu không tự mở tài khoản `DISABLED`.
- Gửi và tra cứu mã công khai không trả hồ sơ cũ và không tiết lộ tài khoản có tồn tại. Gửi email lỗi thì không tạo đơn hay tài khoản và không báo đã gửi thành công.
- **Liên kết thiết lập mật khẩu:** gửi khi nhân viên tạo tài khoản khách (BR-11). Mã ngẫu nhiên 256 bit, chỉ lưu dạng băm, hết hạn sau 24 giờ, dùng một lần, gắn với email và mục đích thiết lập mật khẩu, không dùng chéo; nhập sai không tăng bộ đếm sai mã. Khách tự yêu cầu gửi lại: mã mới thay mã cũ; sớm nhất sau 60 giây và tối đa 5 lần trong 1 giờ cho mỗi email, vượt giới hạn thì trả yêu cầu thử lại sau. Phản hồi gửi lại luôn như nhau, không tiết lộ tài khoản có tồn tại. Mật khẩu mới theo quy tắc Mật khẩu ở trên; thành công thì tài khoản chuyển `ACTIVE`. Gửi email lỗi thì không tạo tài khoản và không báo đã gửi thành công.

#### BR-27 Phân quyền và quản trị tài khoản

- Nhân viên chỉ quản lý tài khoản khách và khóa/mở khóa theo BR-12; không xóa tài khoản, đổi loại tài khoản hay cấp quyền nội bộ.
- QTV quản lý mọi tài khoản, xóa tài khoản theo BR-15 và đổi quyền Nhân viên ↔ QTV trên hồ sơ Nhân viên. Không chuyển tài khoản khách thành nhân viên hoặc ngược lại.
- Không cho khóa, xóa hay hạ quyền làm mất QTV `ACTIVE` cuối cùng (chỉ tính QTV còn tài khoản `ACTIVE`). Khóa tự động ở lần sai thứ 5 vẫn áp dụng cho QTV cuối; khi không còn QTV đăng nhập được, khôi phục bằng quy trình vận hành có người thẩm quyền mở khóa, xác minh danh tính và lưu dấu vết, không tự mở theo thời gian.
- Máy chủ kiểm tra quyền hiện tại, không tiếp tục dùng quyền cũ sau khi thay đổi.
- Khóa hoặc xóa tài khoản không hủy đặt sân hay hóa đơn.
- Tài khoản QTV đầu tiên được cấp qua quy trình triển khai, không tự đăng ký công khai; tài liệu không chứa thông tin đăng nhập mặc định.
- Hồ sơ nhân viên khi tạo phải gắn tài khoản nội bộ, có email và đồng bộ với email đăng nhập. Sau khi xóa tài khoản, hồ sơ vẫn được giữ để bảo toàn lịch sử. Số điện thoại không phải khóa duy nhất.

### 3.6 Lịch sử, nhật ký, thông báo và hiển thị

#### BR-13 Lịch sử trạng thái

- Tạo đơn ghi trạng thái cũ để trống và trạng thái khởi tạo. Mỗi lần đổi trạng thái ghi trạng thái cũ/mới, tác nhân (khách hàng, nhân viên hoặc hệ thống), mã người thực hiện khi có, thời điểm và lý do.
- Ghi trong cùng giao dịch với thay đổi. Mã nhân viên trống không có nghĩa là hệ thống, vì khách cũng tự hủy.
- Lịch sử chỉ đọc. Lặp lại yêu cầu đã thành công không ghi thêm lần chuyển trạng thái.

#### BR-14 Nhật ký

- Hệ thống tự ghi mọi thao tác thêm/sửa/xóa trên sân, lịch, giá, hồ sơ khách, tài khoản, đặt sân, hóa đơn. Mỗi bản ghi gồm tác nhân, đối tượng, giá trị trước/sau và thời điểm.
- Ghi cả xóa tài khoản, khóa/mở khóa, phân quyền, thu/hoàn/hủy hóa đơn và tác vụ tự xử lý.
- Nhật ký xóa tài khoản giữ mã tài khoản và thông tin được phép trong giá trị trước, không phụ thuộc tài khoản còn tồn tại.
- Không ghi mật khẩu, mã xác minh, mã phiên hay giá trị băm của chúng.
- Nhật ký bất biến: không ai được tạo thủ công, sửa hoặc xóa, và hệ thống không nhận dữ liệu nhật ký trực tiếp từ người dùng. Nhật ký không có trường lý do.

#### BR-28 Hiển thị, thông báo và trợ lý AI

- **Hiển thị:** lưới công khai chỉ cho biết khả dụng, đơn giá và bận/trống; không lộ người đặt hay thông tin hóa đơn. Nhân viên xem dữ liệu phục vụ xử lý; khách đã đăng nhập chỉ xem hồ sơ và đơn của mình.
- **Thông báo:** khi tạo, đổi lịch hoặc đổi trạng thái, hệ thống lưu bền vững yêu cầu thông báo cùng giao dịch và chỉ gửi email sau khi giao dịch thành công. Gửi thất bại thì ghi lỗi và thử lại; không hoàn tác nghiệp vụ, không tạo lại đơn hay hóa đơn. Cơ chế gửi ít nhất một lần, khách có thể nhận trùng.
- **Trợ lý AI:** chỉ đọc thông tin công khai để tư vấn, gợi ý. Không tự đặt, giữ, hủy, đổi sân, thu tiền, mở khóa hay đọc dữ liệu cá nhân. Giá và chỗ trống AI gợi ý phải được kiểm tra lại khi đặt. Khi không có dữ liệu phù hợp, AI nói rõ và hướng khách đến nhân viên.

### 3.7 Chuyển trạng thái đặt sân

Thao tác gửi lặp được xử lý theo BR-21 trước khi thực hiện chuyển trạng thái mới. Hoàn thành chỉ xảy ra qua chốt BR-17.

| Trạng thái hiện tại | Thao tác | Điều kiện | Kết quả |
| --- | --- | --- | --- |
| Chưa có đơn | Khách/nhân viên tạo đơn | BR-01, BR-02, BR-04, BR-10, BR-20 | Chờ xác nhận; chưa có hóa đơn |
| Chờ xác nhận | Nhân viên xác nhận | Chưa đến giờ bắt đầu; sân Hoạt động, lịch khả dụng; không trùng | Đã xác nhận; lập hóa đơn Chưa thanh toán hoặc Đã thanh toán |
| Chờ xác nhận | Nhân viên từ chối | Có lý do; còn Chờ xác nhận sau chốt BR-17 | Đã hủy; không lập hóa đơn |
| Chờ xác nhận hoặc Đã xác nhận | Hủy theo yêu cầu khách | BR-05; đơn đã thanh toán phải qua nhân viên (BR-23) | Đã hủy; hóa đơn chưa thu thành `CANCELLED`, đã thu thành `REFUNDED` |
| Chờ xác nhận hoặc Đã xác nhận | Đổi lịch | BR-05; lịch và giá mới hợp lệ; hóa đơn theo BR-22, BR-23 | Giữ trạng thái, mã đơn, chủ hồ sơ; đổi lịch và giá |
| Chờ xác nhận hoặc Đã xác nhận | Nhân viên yêu cầu Hoàn thành | BR-17 | Trước giờ kết thúc: từ chối. Từ giờ kết thúc: chốt hệ thống; trả xung đột để tải lại nếu vừa được chốt |
| Chờ xác nhận hoặc Đã xác nhận | Nhân viên hủy do sự cố | BR-24; khách chưa sử dụng lượt sân | Đã hủy; đồng bộ hóa đơn, hoàn tiền |
| Chờ xác nhận hoặc Đã xác nhận | Chốt giờ kết thúc | Có hóa đơn hợp lệ Đã thanh toán | Hoàn thành tự động; giữ hóa đơn |
| Chờ xác nhận hoặc Đã xác nhận | Chốt giờ kết thúc | Không có hóa đơn hợp lệ Đã thanh toán | Đã hủy; hủy hóa đơn Chưa thanh toán nếu có |
| Hoàn thành | Thu tiền còn thiếu | Hóa đơn Chưa thanh toán; thu đủ tổng tiền | Giữ Hoàn thành; hóa đơn Đã thanh toán |
| Hoàn thành hoặc Đã hủy | Đổi lịch, xác nhận hoặc chuyển trạng thái khác | Không cho phép, kể cả QTV | Giữ trạng thái cuối; trả lỗi nghiệp vụ |

### 3.8 Thứ tự kiểm tra và kết quả lỗi

Áp dụng chung cho khách và nhân viên. Không trả lỗi làm lộ dữ liệu trước khi xác định quyền.

| Thứ tự | Kiểm tra | Kết quả nếu không đạt |
| --- | --- | --- |
| 1 | Phiên, quyền vai trò; quyền sở hữu đơn/hồ sơ nếu là khách | Yêu cầu đăng nhập hoặc từ chối truy cập. Mã đơn của người khác trả Không tìm thấy, không kèm thông tin đơn |
| 2 | Cú pháp dữ liệu; mã gửi lặp và kết quả cũ | Nêu trường không hợp lệ. Cùng mã khác nội dung: từ chối. Cùng mã cùng nội dung: trả kết quả đã lưu (sau khi kiểm tra quyền) |
| 3 | Bản ghi tồn tại; chốt đơn đã đến giờ kết thúc (BR-17); đối chiếu trạng thái dự kiến | Không tìm thấy hoặc Dữ liệu đã thay đổi. Chốt độc lập được lưu trước khi tiếp tục, không hoàn tác nếu yêu cầu bị từ chối |
| 4 | Thao tác đã hoàn tất trên trạng thái hiện tại chưa | Hủy lại đơn Đã hủy, xác nhận lại đơn Đã xác nhận, thu lại cùng nội dung: trả kết quả hiện có, không đổi dữ liệu hay lịch sử. Với thu tiền phải đối chiếu định danh hóa đơn và thông tin thu; không áp dụng cho yêu cầu sửa nội dung khác |
| 5 | Chuyển trạng thái có hợp lệ | Trạng thái không cho phép thao tác; đơn Đã hủy không được thu, xác nhận hay Hoàn thành |
| 6 | Mốc giờ thao tác và điều kiện sử dụng thực tế | Quá hạn hủy/đổi, chưa kết thúc lượt, đã đến giờ bắt đầu, hoặc cần xử lý qua nhân viên |
| 7 | Sân chưa xóa và Hoạt động khi cần; lịch, giờ mới khả dụng và đúng quy tắc | Sân/lịch không khả dụng hoặc giờ không hợp lệ |
| 8 | Không trùng lịch; báo giá; tình trạng hóa đơn và xử lý tiền | Slot đã được đặt; cần xác nhận báo giá mới; chưa hoàn tất xử lý tiền. Giữ nguyên dữ liệu của thao tác thất bại |
| 9 | Ràng buộc duy nhất và chống xung đột ngay khi lưu; ghi đủ lịch sử, nhật ký | Hoàn tác thao tác đang lưu, trả dữ liệu cần tải lại; không báo thành công một phần |

Khi có thao tác cạnh tranh, lỗi ở bước 8 có thể chỉ lộ ra ở bước 9, nên bước 9 là kiểm tra cuối cùng bắt buộc. Nếu đơn vừa được Hoàn thành tự động, yêu cầu xác nhận cũ không được áp lại.

### 3.9 Cách đọc trạng thái thanh toán

| Trạng thái đặt sân | Hóa đơn | Hiển thị và thao tác |
| --- | --- | --- |
| Chờ xác nhận | Chưa có | Hiển thị "Chưa lập hóa đơn"; không ghi Đã thanh toán |
| Đã xác nhận | Chưa thanh toán hoặc Đã thanh toán | Tách nhãn xác nhận đặt sân và nhãn thanh toán; có đúng một hóa đơn hợp lệ |
| Hoàn thành | Chưa thanh toán | Còn khoản cần thu (dữ liệu từ trước); thu được sau theo BR-09. Chốt không tạo thêm trường hợp này |
| Hoàn thành | Đã thanh toán | Đã thu đủ; chốt tự động không chứng minh khách có mặt |
| Đã hủy | Chưa có, hoặc Đã hủy/Đã hoàn tiền | Không nhận tiền, không lập hóa đơn mới; thông tin thu/hoàn cũ chỉ để tra cứu |

## 4. Yêu cầu chức năng

Mỗi yêu cầu nêu việc hệ thống phải làm; điều kiện chi tiết nằm ở quy tắc nghiệp vụ được dẫn chiếu (mục 3).

### 4.1 Chức năng khách hàng

| Mã | Yêu cầu | Story |
| --- | --- | --- |
| KH-01 | Khách đăng ký tài khoản bằng họ tên, số điện thoại, email và mật khẩu (BR-26). Email là tên đăng nhập và không trùng tài khoản khác. Nếu đã có hồ sơ khách vãng lai cùng email thì liên kết tài khoản mới với hồ sơ đó (BR-11). | US-1 |
| KH-02 | Gửi mã xác minh email đăng ký; xử lý hết hạn, gửi lại, nhập sai theo BR-26. Chỉ tạo tài khoản và liên kết hồ sơ sau khi xác minh thành công. | US-1 |
| KH-03 | Đăng nhập bằng email và mật khẩu. Tài khoản `DISABLED`, `PENDING_ACTIVATION` hoặc đã xóa không đăng nhập được (BR-12). | US-1 |
| KH-04 | Quên mật khẩu: xác minh email rồi đặt mật khẩu mới (BR-26); không tự mở tài khoản `DISABLED` và không áp dụng cho tài khoản `PENDING_ACTIVATION`. | US-1 |
| KH-05 | Hiển thị danh sách sân gồm tên, mô tả, trạng thái, không yêu cầu đăng nhập; không hiển thị sân Đã xóa. | US-2 |
| KH-06 | Hiển thị lưới theo ngày, mỗi slot 30 phút trong giờ mở cửa. Chờ xác nhận, Đã xác nhận, Hoàn thành giữ chỗ; Đã hủy không giữ chỗ. Slot trống màu xanh và có nhãn; slot bận hoặc sân/lịch không khả dụng được phân biệt bằng nhãn, không chỉ bằng màu. Không hiển thị sân Đã xóa hay thông tin cá nhân người đặt (BR-28). | US-2 |
| KH-07 | Hiển thị đơn giá theo khung giờ của từng sân. | US-2 |
| KH-08 | Khách thành viên chọn sân, ngày, giờ bắt đầu và kết thúc (BR-20); máy chủ trả báo giá (BR-04). Xem giá hoặc chọn slot không giữ chỗ. | US-3 |
| KH-09 | Đặt sân thành công tạo đơn Chờ xác nhận, có mã đặt sân (BR-07) hiển thị cho khách và lưu trong lịch sử đặt sân. | US-3 |
| KH-10 | Từ chối đặt sân và nêu lý do khi vi phạm BR-01, BR-02, BR-20, khi sân không Hoạt động, hồ sơ/tài khoản không hợp lệ hoặc báo giá đã đổi. Xung đột không tạo đơn một phần. | US-3 |
| KH-11 | Đặt nhanh bằng họ tên, số điện thoại, email và sân/ngày/giờ, không cần tài khoản. Xác minh email (BR-26) trước khi lưu; tra cứu và dùng lại hồ sơ theo email, chỉ tạo hồ sơ mới khi email chưa có (BR-10). Thành công trả mã đặt sân mới, không trả dữ liệu cũ của hồ sơ. | US-4 |
| KH-12 | Hủy đặt sân. Khách thành viên tự hủy đơn chưa thanh toán của mình tại màn hình lịch sử đặt sân (My Bookings); khách vãng lai và đơn đã thanh toán đi qua nhân viên (hotline hoặc tại cơ sở). Điều kiện theo BR-05; hóa đơn theo BR-22, BR-23. Hủy thành công: đơn Đã hủy, ghi lịch sử và nhật ký đúng tác nhân, giải phóng slot, thông báo khách (BR-28). Hủy lại đơn Đã hủy trả kết quả Đã hủy, không xử lý tiền hay ghi chuyển trạng thái lần nữa. | US-5 |
| KH-13 | Đổi lịch đặt sân. Khách thành viên tự đổi sân, ngày hoặc giờ của đơn chưa thanh toán; khách vãng lai và đơn đã thanh toán làm việc với nhân viên. Đổi lịch có hiệu lực ngay, theo BR-05 và BR-22: xem báo giá, xác nhận tổng tiền rồi lưu. Kết quả giữ mã đặt sân, trạng thái và chủ hồ sơ; giải phóng slot cũ, giữ slot mới, đồng bộ hóa đơn (BR-22, BR-23). Đổi thất bại giữ nguyên lịch, giá và hóa đơn cũ. | US-6 |
| KH-14 | Khách xem đơn của mình gồm mã, sân, ngày/giờ, tổng tiền, trạng thái sử dụng và trạng thái thanh toán (hiển thị riêng). Sắp xếp mới nhất trước (ngày/giờ sử dụng, sau đó thời điểm tạo); có phân trang và xem chi tiết. Hồ sơ vãng lai đã liên kết sau đăng ký vẫn hiện đủ lịch sử cũ. | US-7 |
| KH-15 | Khách thành viên xem thông tin cá nhân và chỉ tự cập nhật họ tên, số điện thoại. Email chỉ xem, không đổi (BR-18). | US-8 |
| KH-16 | Cung cấp khung trò chuyện với trợ lý AI, dùng được khi chưa đăng nhập. | US-9 |
| KH-17 | Trợ lý AI tư vấn sân và dịch vụ, hướng dẫn đặt sân, gợi ý sân/giờ theo dữ liệu công khai và giải đáp câu hỏi thường gặp; không thực hiện thao tác nghiệp vụ hay truy cập dữ liệu cá nhân (BR-28). | US-9 |
| KH-18 | Khách có tài khoản do nhân viên tạo đặt mật khẩu qua liên kết trong email (hiệu lực 24 giờ, dùng một lần). Liên kết hết hạn hoặc đã bị thay: khách tự yêu cầu gửi liên kết mới, hồ sơ và lịch sử đặt sân giữ nguyên (BR-11, BR-26). | US-15 |

### 4.2 Chức năng nhân viên

| Mã | Yêu cầu | Story |
| --- | --- | --- |
| NV-01 | Nhân viên đăng nhập bằng tài khoản do QTV cấp; không có chức năng tự đăng ký. Mọi chức năng nhân viên chỉ dùng được sau khi đăng nhập và đúng quyền (mục 2.2). | US-10 |
| NV-02 | Danh sách đơn Chờ xác nhận gồm mã, khách, điện thoại, sân, ngày/giờ, tổng tiền, thời điểm tạo; yêu cầu cũ nhất trước, có phân trang; xem chi tiết từng yêu cầu. | US-11 |
| NV-03 | Xác nhận đơn Chờ xác nhận trước giờ bắt đầu: kiểm tra sân, lịch, không trùng, rồi tạo hóa đơn (BR-08). Đã xác nhận nghĩa là nhân viên đã xác nhận, không phải đã thu tiền. Xác nhận lặp trả kết quả hiện có. | US-12 |
| NV-04 | Từ chối đơn Chờ xác nhận kèm lý do: chuyển Đã hủy, giải phóng chỗ, thông báo khách. Đơn Đã xác nhận muốn hủy phải theo BR-05 hoặc BR-24, không dùng thao tác từ chối. | US-12 |
| NV-05 | Yêu cầu chuyển Hoàn thành chỉ được thực hiện qua chốt BR-17: trước giờ kết thúc bị từ chối; từ giờ kết thúc, hệ thống chốt theo thanh toán. Không có ngoại lệ thủ công hay lập hóa đơn để cứu đơn. Đơn đã Hoàn thành trả trạng thái hiện có; đơn Đã hủy không hồi sinh. | US-12 |
| NV-06 | Xem lịch đặt sân theo ngày dạng lưới, gồm cả slot trống; lọc theo sân. | US-13 |
| NV-07 | Tạo đặt sân thay khách tại quầy hoặc hotline: dùng hồ sơ theo email đã xác minh hoặc tạo hồ sơ mới (không thuộc nhân viên). Chỉ tạo trước giờ bắt đầu, khởi tạo Chờ xác nhận và xác nhận qua NV-03; áp dụng BR-02, BR-20. | US-13 |
| NV-08 | Đổi hoặc hủy thay khách theo BR-05, BR-22, BR-23. Hủy do sự cố theo BR-24, bắt buộc có lý do. Không sửa trực tiếp trạng thái để bỏ qua quy tắc. | US-13 |
| NV-09 | Cập nhật trạng thái sân chưa xóa: Hoạt động (`ACTIVE`), Bảo trì (`MAINTENANCE`), Đóng (`CLOSED`). Đã xóa (`DELETED`) chỉ đặt qua chức năng xóa sân của QTV (QT-04). Sân không Hoạt động không nhận đặt mới, đổi lịch vào sân hay xác nhận thông thường. Chuyển Bảo trì/Đóng bị từ chối khi còn đơn chưa xử lý (BR-16). | US-14 |
| NV-10 | Xem danh sách khách hàng; tìm theo họ tên, số điện thoại, email. | US-15 |
| NV-11 | Xem và cập nhật thông tin khách hàng; xem các đặt sân của khách. Không đổi email (BR-18). | US-15 |
| NV-12 | Tạo tài khoản khách hàng: dùng lại hồ sơ chưa liên kết nếu email đã có, nếu chưa có thì tạo hồ sơ mới; tài khoản ở trạng thái Chờ kích hoạt và hệ thống gửi liên kết thiết lập mật khẩu đến email khách (BR-11, BR-26). | US-15 |
| NV-13 | Quản lý tài khoản khách hàng: xem, cập nhật thông tin, khóa, mở khóa (BR-12). Không đổi email (BR-18). Không quản lý tài khoản nội bộ hay phân quyền. | US-15 |
| NV-14 | Hệ thống tự lập hóa đơn theo BR-08. Hóa đơn gồm mã, mã đặt sân, tổng tiền, trạng thái thanh toán, nhân viên lập (có thể trống khi khách tự đổi lịch), thời điểm lập, ghi chú; khi Đã thanh toán lưu thêm số tiền, thời điểm thực thu, phương thức, người xác nhận. | US-16 |
| NV-15 | Xem danh sách hóa đơn có phân trang; lọc theo trạng thái (`UNPAID`, `PAID`, `CANCELLED`, `REFUNDED`), tìm theo mã. Xem chi tiết: hóa đơn tiền nhiệm/thay thế, chứng cứ thu/hoàn/hủy, lý do, tác nhân, thời điểm. Hóa đơn được điều chỉnh cùng việc đổi/hủy đơn (BR-22, BR-23). Đơn Đã xác nhận hoặc Hoàn thành luôn có đúng một hóa đơn hợp lệ; đơn Chờ xác nhận chưa có hóa đơn. | US-16 |
| NV-16 | Nhân viên/QTV ghi nhận thu đủ tổng tiền hóa đơn (BR-09): `UNPAID` → `PAID`. Đơn Hoàn thành chưa thu vẫn thu được; không thu đơn Đã hủy hoặc hóa đơn `CANCELLED`/`REFUNDED`. Áp dụng BR-17 trước thao tác (chốt đã lưu dù thu bị từ chối). Thu lặp đúng hóa đơn và nội dung trả kết quả cũ, không đổi chứng cứ, thời điểm hay chuyển tiền lại. | US-16 |
| NV-17 | Tìm nhanh theo số điện thoại, email hoặc mã đặt sân; trả về khách hàng và các đặt sân liên quan kèm trạng thái, mở chi tiết ngay. | US-17 |
| NV-18 | Hiển thị số điện thoại hotline. Nhân viên hỗ trợ khách qua điện thoại bên ngoài hệ thống. | US-18 |

### 4.3 Chức năng quản trị viên

Quản trị viên dùng được toàn bộ chức năng ở mục 4.2 và các chức năng dưới đây.

| Mã | Yêu cầu | Story |
| --- | --- | --- |
| QT-01 | Đăng nhập khu vực quản trị bằng tài khoản có quyền quản trị; tài khoản không có quyền bị từ chối truy cập. | US-19 |
| QT-02 | Thêm sân: tên bắt buộc, không trùng tên sân chưa xóa; mô tả; trạng thái Hoạt động, Bảo trì hoặc Đóng. | US-20 |
| QT-03 | Sửa thông tin sân chưa xóa; tên bắt buộc, không trùng tên sân chưa xóa khác. | US-20 |
| QT-04 | Xóa mềm sân theo BR-19. Không xóa khi còn đơn Chờ xác nhận hoặc Đã xác nhận chưa xử lý. | US-20 |
| QT-05 | Tạo sân yêu cầu nhập giờ mở/đóng và đơn giá mặc định; hệ thống tạo 7 lịch và 7 mốc giá trong cùng giao dịch (BR-25). Sửa giờ và khả dụng của từng ngày theo BR-01, BR-16. | US-20 |
| QT-06 | Cấu hình mốc giá theo BR-03; lưu thay toàn bộ bảng giá của ngày. Khi đổi giờ mở/đóng, xem trước cách điều chỉnh giá (BR-25) rồi xác nhận. | US-20 |
| QT-07 | Từ chối thay đổi lịch mở cửa vi phạm BR-16; ngày ngừng mở cửa được đánh dấu không khả dụng (BR-01). Đổi bảng giá không đổi tổng tiền của các đặt sân đã tạo (BR-04). | US-20 |
| QT-08 | Tạo tài khoản cho khách hàng, nhân viên, quản trị viên. | US-21 |
| QT-09 | Cập nhật thông tin tài khoản trong phạm vi loại hồ sơ hiện có (BR-27). Không đổi email hồ sơ hay email đăng nhập (BR-18). Không sửa trực tiếp bộ đếm, mật khẩu băm hay trạng thái để bỏ qua chức năng khóa/mở khóa. | US-21 |
| QT-10 | Khóa/mở khóa tài khoản khách hàng, nhân viên, QTV (BR-12). Xóa tài khoản theo BR-15, kể cả tài khoản khách; giữ hồ sơ và lịch sử, ghi nhật ký, bảo vệ QTV `ACTIVE` cuối cùng. | US-21 |
| QT-11 | Đổi quyền Nhân viên ↔ QTV trên tài khoản nội bộ (BR-27); không chuyển loại hồ sơ Khách hàng ↔ Nhân viên. Bảo vệ QTV `ACTIVE` cuối cùng. | US-21 |
| QT-12 | Thêm nhân viên gồm hồ sơ (họ tên, số điện thoại, email, có quyền quản trị hay không) và tài khoản đăng nhập. | US-22 |
| QT-13 | Sửa họ tên, số điện thoại hồ sơ nhân viên; không đổi email (BR-18). Đổi quyền theo QT-11. | US-22 |
| QT-14 | Xóa tài khoản nhân viên theo BR-15 và BR-27: giữ hồ sơ và mã nhân viên cho lịch sử, hóa đơn, nhật ký; không xóa hồ sơ hay giao dịch khách. | US-22 |
| QT-15 | Xem toàn bộ đặt sân của hệ thống; lọc theo ngày, sân, trạng thái, khách hàng; xem chi tiết kèm lịch sử trạng thái. | US-23 |
| QT-16 | Thống kê doanh thu theo khoảng thời gian QTV chọn: tổng doanh thu và phân theo ngày hoặc tháng, dạng bảng và biểu đồ. | US-24 |
| QT-17 | Doanh thu là tổng tiền các hóa đơn hợp lệ Đã thanh toán có thời điểm thu trong khoảng lọc. Không cộng hóa đơn Đã hủy, Đã hoàn tiền, hay bản thay thế chưa thu. Hóa đơn đã hoàn bị loại kể cả khi thời điểm thu thuộc kỳ cũ, nên báo cáo kỳ cũ có thể đổi sau hoàn tiền; chỉ tiêu này không phải báo cáo dòng tiền thu/hoàn. | US-24 |
| QT-18 | Thống kê lượt đặt và giờ đã đặt (mọi trạng thái trừ Đã hủy), tách lượt và giờ Hoàn thành. Lọc theo ngày sử dụng; giữ giao dịch của sân Đã xóa với nhãn lịch sử. Chỉ tiêu Hoàn thành gồm cả chốt tự động (BR-17), không xác nhận khách có mặt. | US-25 |
| QT-19 | Xem nhật ký hoạt động gồm thời điểm, tác nhân (hệ thống, nhân viên, khách hàng), thao tác, đối tượng, giá trị trước và sau. | US-26 |
| QT-20 | Lọc nhật ký theo thời gian, tác nhân, đối tượng, thao tác. Nhật ký chỉ đọc (BR-14). | US-26 |

## 5. Yêu cầu dữ liệu

Bảng dưới liệt kê dữ liệu hệ thống phải lưu. Cấu trúc bảng, kiểu dữ liệu và ràng buộc chi tiết nằm trong `database.md`.

| Dữ liệu | Thông tin lưu trữ |
| --- | --- |
| Tài khoản | Mã tài khoản, email đăng nhập chuẩn hóa (duy nhất), mật khẩu dạng băm, loại (Khách hàng, Nhân viên), trạng thái (`ACTIVE`, `DISABLED`, `PENDING_ACTIVATION`), số lần đăng nhập sai liên tiếp. |
| Nhân viên | Họ tên, số điện thoại, email, quyền quản trị, tài khoản liên kết. |
| Khách hàng | Họ tên, số điện thoại, email (duy nhất), tài khoản liên kết (có thể trống; đặt nhanh có thể dùng lại hồ sơ đã liên kết tài khoản, BR-10). |
| Mã xác minh | Email chuẩn hóa, mục đích, mã băm, số lần sai, đã dùng chưa, thời điểm hết hạn. |
| Sân | Mã sân, tên (duy nhất; bắt buộc khi chưa xóa; `NULL` khi Đã xóa), mô tả, trạng thái (`ACTIVE`, `MAINTENANCE`, `CLOSED`, `DELETED`). |
| Lịch mở cửa | Sân, ngày trong tuần, giờ mở, giờ đóng, khả dụng; duy nhất theo sân và ngày trong tuần (BR-01). |
| Bảng giá | Sân, ngày trong tuần, mốc giờ, đơn giá theo giờ > 0; duy nhất theo sân, ngày trong tuần và mốc giờ (BR-03). |
| Đặt sân | Mã đặt sân, khách hàng, sân, ngày sử dụng, giờ bắt đầu, giờ kết thúc, trạng thái (`PENDING`, `CONFIRMED`, `COMPLETED`, `CANCELLED`), tổng tiền VND đã làm tròn, các mốc và đơn giá đã áp dụng. |
| Lịch sử trạng thái đặt sân | Đặt sân, trạng thái cũ (trống khi tạo), trạng thái mới, loại tác nhân, mã khách hoặc nhân viên khi có, thời điểm, lý do. Mỗi bản ghi chỉ có một loại người thực hiện. |
| Hóa đơn | Mã hóa đơn, đặt sân, tổng tiền, trạng thái (`UNPAID`, `PAID`, `CANCELLED`, `REFUNDED`), nhân viên lập (có thể trống), tác nhân thực tế, người xác nhận thu/hủy/hoàn, thời điểm lập/thu/hủy/hoàn (UTC), phương thức và số tiền thu/hoàn, lý do, hóa đơn tiền nhiệm/thay thế, ghi chú. Giữ chứng cứ thu cũ sau hoàn tiền. |
| Nhật ký hệ thống | Tác nhân (hệ thống, nhân viên, khách hàng), mã người thực hiện, thao tác, đối tượng, mã đối tượng, giá trị trước/sau đã loại bí mật, thời điểm. Bất biến (BR-14). |
| Chống gửi lặp | Mã yêu cầu, phạm vi người thao tác, định danh nội dung thao tác, kết quả đã lưu, mã bản ghi liên quan (BR-21). |
| Thông báo | Đối tượng nhận, mã đơn/sự kiện, trạng thái gửi, số lần thử, lỗi gần nhất; không chứa mật khẩu hay mã phiên. |

## 6. Yêu cầu phi chức năng

| Mã | Nhóm | Yêu cầu |
| --- | --- | --- |
| NFR-01 | Bảo mật | Mật khẩu, mã xác minh và liên kết thiết lập mật khẩu chỉ lưu dạng băm, không lưu bản rõ. |
| NFR-02 | Bảo mật | Quyền được kiểm tra tại máy chủ theo mục 2.2, không chỉ dựa vào việc ẩn chức năng trên giao diện. Mọi API đọc/sửa dữ liệu khách kiểm tra chủ sở hữu; mọi yêu cầu cần đăng nhập kiểm tra tài khoản còn tồn tại, `ACTIVE` và quyền hiện tại (BR-12, BR-27). Không ghi bí mật vào log; không dùng AI để vượt quyền. |
| NFR-03 | Toàn vẹn dữ liệu | Chống trùng lịch (BR-02) được bảo đảm ở tầng cơ sở dữ liệu, kể cả khi nhiều người đặt cùng một sân tại cùng thời điểm. |
| NFR-04 | Toàn vẹn dữ liệu | Không xóa vật lý dữ liệu giao dịch và sân; xóa mềm sân giải phóng tên (BR-15, BR-19). |
| NFR-05 | Truy vết | Hệ thống tự ghi nhật ký cho mọi thao tác theo BR-14; không ai, kể cả nhân viên và QTV, được sửa hoặc xóa nhật ký. |
| NFR-06 | Thời gian | Mốc thời gian hệ thống (tạo, cập nhật, nhật ký, hết hạn mã xác minh, thực thu/hoàn) lưu theo UTC. Ngày/giờ sử dụng và mốc chốt giờ kết thúc dùng giờ Việt Nam (UTC+07:00) theo BR-17. |
| NFR-07 | Toàn vẹn dữ liệu | Giao dịch nguyên tử, duy nhất email/tài khoản/hóa đơn, kiểm soát xung đột và gửi lặp (BR-21) được bảo đảm phía máy chủ, không dựa vào nút bị vô hiệu trên trình duyệt. |
| NFR-08 | Khả năng phục hồi | Chốt giờ kết thúc (BR-17) chạy bù khi lỗi và không đổi trạng thái cuối. Email được lưu bền vững, gửi sau giao dịch và thử lại; có thể trùng, không hoàn tác nghiệp vụ (BR-28). |
| NFR-09 | Khả năng kiểm thử | Thời điểm nghiệp vụ lấy từ một đồng hồ máy chủ thống nhất, để kiểm thử được các biên: đúng 1 giờ, đúng giờ kết thúc, lịch chạy :01/:31 giờ Việt Nam, và các thao tác cạnh tranh. |

## 7. Thay đổi tài liệu

| Phiên bản | Thay đổi |
| --- | --- |
| 1.2 | Làm rõ vòng đời đặt sân, hóa đơn, xác minh email và xử lý đồng thời. |
| 1.3 | Bỏ tạo đơn cho lượt đã diễn ra. |
| 1.4 | Gộp khóa/vô hiệu hóa thành `DISABLED`; xóa tài khoản vĩnh viễn; bỏ quản lý phiên. |
| 1.5 | Rút gọn: bỏ lịch sử gửi mã, lịch mở kế hoạch, tỷ lệ lấp đầy. |
| 1.6 | Cấm đổi mọi email; nhật ký do hệ thống tự ghi, bất biến. |
| 1.7 | Bỏ trường lý do khỏi nhật ký. |
| 1.8 | Đổi lịch có hiệu lực ngay (báo giá, chống xung đột, gửi lặp); thu/hoàn và hóa đơn thay thế; chốt từ giờ kết thúc; email bền vững; trạng thái dự kiến khi hủy; thời điểm thu/hoàn UTC không dung sai. |
| 2.0 · 28/10/2026 | Viết lại để dễ đọc, không đổi nghiệp vụ: tách quy tắc dài thành điều khoản ngắn và nhóm theo chủ đề; định nghĩa quy tắc xử lý chung một lần (BR-21); gộp yêu cầu chức năng trùng lặp (KH 23→17, NV 24→18, QT 21→20) và đánh số lại KH/NV/QT/NFR; chuyển chi tiết thiết kế (tên trường, cách lưu) sang `database.md`. |
| 2.1 · 30/10/2026 | Thêm trạng thái `PENDING_ACTIVATION` và liên kết thiết lập mật khẩu 24 giờ cho tài khoản khách do nhân viên tạo (BR-11, BR-12, BR-26, KH-03, KH-04, KH-18, NV-12, NFR-01). |
