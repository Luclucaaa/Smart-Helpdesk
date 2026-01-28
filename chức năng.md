Đây là bản Tài liệu Đặc tả Chức năng (Functional Specification Document) dành riêng cho việc triển khai mã nguồn (coding). Bạn có thể dùng tài liệu này làm kim chỉ nam trong quá trình phát triển 12 tuần tới.
Tài liệu được thiết kế theo mô hình Centralized Gateway (Một cổng tiếp nhận cho nhiều sản phẩm) kết hợp AI Core.
________________________________________
TÀI LIỆU ĐẶC TẢ HỆ THỐNG
Tên dự án: Smart Centralized Helpdesk (Cổng Hỗ trợ Khách hàng Thông minh)
Mô hình: Multi-Product Support + AI Automation
________________________________________
1. TỔNG QUAN LUỒNG DỮ LIỆU (SYSTEM FLOW)
1.	Khách hàng: Chọn sản phẩm đang dùng $\rightarrow$ Nhập nội dung sự cố (tự do).
2.	AI Engine: Đọc nội dung $\rightarrow$ Phân tích cảm xúc (Tức giận/Bình thường) $\rightarrow$ Phân loại vấn đề (Lỗi/Góp ý/Mua hàng).
3.	Router (Bộ điều phối): Dựa trên kết quả AI $\rightarrow$ Gán độ ưu tiên $\rightarrow$ Chuyển về Dashboard của team phụ trách.
4.	Nhân viên: Nhận thông báo $\rightarrow$ Xử lý $\rightarrow$ Phản hồi.
________________________________________
2. CHI TIẾT CÁC PHÂN HỆ (MODULES)
MODULE A: CỔNG KHÁCH HÀNG (CUSTOMER PORTAL)
Mục tiêu: Đơn giản hóa tối đa, tạo cảm giác được lắng nghe.
1. Màn hình Gửi Yêu cầu (Smart Submission Form)
•	Input (Khách nhập):
o	Dropdown "Sản phẩm": (Ví dụ: App Bán Hàng, Web Kế Toán, App Kho). Lý do: Để biết chuyển cho team nào.
o	Text Area "Mô tả vấn đề": (Một ô lớn duy nhất). Placeholder: "Hãy kể cho chúng tôi nghe vấn đề của bạn..."
o	File đính kèm: (Ảnh lỗi).
o	(Ẩn hoàn toàn các ô Priority, Category, Assignee).
•	Xử lý ngầm (Backend):
o	Gọi API ML.NET để lấy kết quả phân tích từ Text Area.
•	Output (Hiển thị ngay sau khi gửi):
o	Hiện thông báo kết quả AI phân tích (để khách yên tâm): "Hệ thống xác định đây là Lỗi kỹ thuật nghiêm trọng. Đã chuyển ngay cho Team Dev App Bán Hàng."
2. Màn hình Theo dõi (Request Tracking)
•	Hiển thị danh sách yêu cầu của khách.
•	Trạng thái trực quan: Mới $\rightarrow$ Đang xử lý $\rightarrow$ Đã xong.
•	Chat: Khung chat để trao đổi thêm với nhân viên.
________________________________________
MODULE B: BỘ NÃO AI (AI CORE - ML.NET)
Mục tiêu: Thay thế con người trong việc đọc hiểu và phân loại.
1. Chức năng Phân tích Cảm xúc (Sentiment Analysis)
•	Đầu vào: Chuỗi văn bản khách hàng nhập.
•	Thuật toán: Binary Classification (Tích cực / Tiêu cực).
•	Logic xử lý:
o	Nếu Score < 0.4 (Tiêu cực/Giận dữ) $\rightarrow$ Set Priority = HIGH (Khẩn cấp).
o	Nếu Score >= 0.4 $\rightarrow$ Set Priority = NORMAL.
2. Chức năng Phân loại Chủ đề (Ticket Categorization)
•	Đầu vào: Chuỗi văn bản.
•	Thuật toán: Multiclass Classification.
•	Nhãn (Label) cần train:
o	Bug (Lỗi phần mềm).
o	Feature (Yêu cầu tính năng mới).
o	Support (Hỗ trợ HDSD/Cài đặt).
o	Sale (Hỏi giá/Mua thêm gói).
________________________________________
MODULE C: KHÔNG GIAN LÀM VIỆC CỦA NHÂN VIÊN (AGENT DASHBOARD)
Mục tiêu: Giúp nhân viên làm việc nhanh, đúng trọng tâm.
1. Danh sách chờ Thông minh (Smart Queue)
•	Thay vì sắp xếp theo Ngày tháng, hệ thống sắp xếp theo Điểm trọng số (Weighted Score):
o	(Công thức: Priority High + Thời gian chờ lâu = Xếp đầu).
•	Giao diện:
o	Các ticket có Sentiment = Negative sẽ có viền đỏ hoặc icon ngọn lửa 🔥.
o	Có bộ lọc nhanh: "Lỗi App Bán Hàng", "Lỗi Web Kế Toán".
2. Màn hình Xử lý chi tiết
•	Hiển thị thông tin AI đã dán nhãn: Category: Bug | Sentiment: Angry.
•	Smart Action (Gợi ý hành động):
o	Nếu là Bug: Hiện nút "Chuyển JIRA cho Dev".
o	Nếu là Sale: Hiện nút "Chuyển cho Sale Team".
•	Trả lời mẫu (Canned Responses): Chọn nhanh câu trả lời dựa trên loại lỗi.
________________________________________
MODULE D: QUẢN TRỊ & CẤU HÌNH (ADMIN PANEL)
Mục tiêu: Quản lý đa sản phẩm (Centralized Management).
1. Quản lý Sản phẩm (Products Management)
•	CRUD (Thêm/Sửa/Xóa) các sản phẩm công ty đang kinh doanh.
•	Ví dụ: Thêm sản phẩm "App Điểm danh", gán nhân viên A, B phụ trách sản phẩm này.
2. Báo cáo Thống kê (Analytics)
•	Biểu đồ tròn: Tỷ lệ lỗi theo từng Sản phẩm (Sản phẩm nào nhiều lỗi nhất?).
•	Biểu đồ cột: Chất lượng phục vụ (Dựa trên đánh giá sao của khách).
________________________________________
3. THIẾT KẾ CƠ SỞ DỮ LIỆU (DATABASE SCHEMA GỢI Ý)
Để triển khai mô hình này, bạn cần bổ sung/chỉnh sửa các bảng trong source code cũ như sau:
Bảng Products (Mới - Để làm cổng giao tiếp đa sản phẩm)
•	Id (INT, PK)
•	Name (NVARCHAR - VD: "App Bán Hàng")
•	Description
Bảng Tickets (Sửa từ bảng cũ)
•	Id, Title, Description, Status (Như cũ)
•	ProductId (FK - Liên kết với bảng Products) $\rightarrow$ Quan trọng.
•	Priority (Enum: Low, Normal, High) $\rightarrow$ Do AI quyết định.
•	Category (Enum: Bug, Feature, Sale) $\rightarrow$ Do AI quyết định.
•	SentimentScore (FLOAT) $\rightarrow$ Lưu điểm số cảm xúc AI chấm.
Bảng TicketComments
•	Lưu nội dung chat qua lại.
________________________________________
4. KẾ HOẠCH TRIỂN KHAI TRÊN CODE (MAPPING)
Khi bạn tải source code MVC về, hãy thực hiện theo thứ tự này:
1.	Bước 1 (Database): Tạo bảng Products, thêm cột Category, Sentiment vào bảng Tickets.
2.	Bước 2 (Giao diện Khách): Sửa view Create.cshtml. Thêm dropdown chọn Product. Ẩn các dropdown khác.
3.	Bước 3 (Backend): Vào TicketsController.cs, hàm Create:
o	Chèn code gọi ML.NET vào trước khi lưu xuống Database.
o	Gán giá trị cho ticket.Category và ticket.Priority dựa trên kết quả AI.
4.	Bước 4 (Giao diện Admin): Sửa view Index.cshtml của Admin để hiển thị cột Sentiment (Vẽ icon mặt cười/mặt mếu) và cho phép lọc theo Product.

