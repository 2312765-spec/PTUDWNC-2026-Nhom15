using Xunit;

// Mỗi test class dùng PostgresApiFactory/RecipeImagesApiFactory tự khởi MỘT Testcontainers
// Postgres riêng. Chạy song song nhiều class cùng lúc (mặc định của xUnit) làm nhiều container
// Postgres khởi động đồng thời, gây quá tải tài nguyên Docker trên máy dev (RAM giới hạn) và
// timeout ngẫu nhiên không liên quan gì tới logic test. Tắt song song ở mức assembly cho toàn
// bộ integration test — chấp nhận chạy chậm hơn để đổi lấy kết quả ổn định.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
