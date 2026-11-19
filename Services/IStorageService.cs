namespace Task6.Services
{
    public interface IStorageService
    {
        Task<string?> UploadImageAsync(Stream fileStream, string fileName, string contentType);

        Task DeleteImageAsync(string imageUrl);
    }
}
