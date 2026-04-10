using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Components.Forms;

namespace ContosoDashboard.Services
{
    /// <summary>
    /// Adapter class to convert Blazor IBrowserFile to ASP.NET Core IFormFile
    /// Used when calling server-side services from Blazor components with uploaded files
    /// </summary>
    public class BrowserFileFormFileAdapter : IFormFile
    {
        private readonly IBrowserFile _browserFile;

        public BrowserFileFormFileAdapter(IBrowserFile browserFile)
        {
            _browserFile = browserFile ?? throw new ArgumentNullException(nameof(browserFile));
        }

        /// <summary>
        /// Gets the raw Content-Disposition header of the uploaded file
        /// </summary>
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";

        /// <summary>
        /// Gets the MIME type of the uploaded file
        /// </summary>
        public string ContentType => _browserFile.ContentType;

        /// <summary>
        /// Gets the file name from the upload request
        /// </summary>
        public string FileName => _browserFile.Name;

        /// <summary>
        /// Gets the HTTP Content-Length header
        /// </summary>
        public long Length => _browserFile.Size;

        /// <summary>
        /// Gets the name from the Content-Disposition header
        /// </summary>
        public string Name => "file";

        /// <summary>
        /// Gets the headers collection for the uploaded file
        /// </summary>
        public IHeaderDictionary Headers => new HeaderDictionary
        {
            { "Content-Disposition", ContentDisposition },
            { "Content-Type", ContentType }
        };

        /// <summary>
        /// Gets the stream to read the file from
        /// </summary>
        public Stream OpenReadStream()
        {
            // Do not cache the stream here because many callers wrap it in a 'using' block,
            // which would dispose the cached stream and make it unusable for future calls.
            // IBrowserFile.OpenReadStream() returns a new stream each time.
            return _browserFile.OpenReadStream(26214400); // 25MB limit
        }

        /// <summary>
        /// Copy the entire file to the target stream
        /// </summary>
        public void CopyTo(Stream target)
        {
            using var stream = OpenReadStream();
            stream.CopyTo(target);
        }

        /// <summary>
        /// Copy the entire request body to the target stream asynchronously
        /// </summary>
        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            using var stream = OpenReadStream();
            await stream.CopyToAsync(target, cancellationToken);
        }
    }
}
