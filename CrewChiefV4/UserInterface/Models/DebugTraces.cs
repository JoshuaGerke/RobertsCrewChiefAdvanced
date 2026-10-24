using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.UserInterface.Models
{
    public class DebugTraces
    {
        /// <summary>
        /// Make a boilerplate email in the default email client,
        /// user then adds anything they want to the email and clicks send
        /// </summary>
        /// <param name="subject"></param>
        /// <param name="body"></param>
        /// <param name="recipient"></param>
        public void FillInEmail(string subject, string body, string recipient)
        {
            string mailtoUrl = $"mailto:{recipient}?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
            string details = $"The file.io URL for the trace file is in\n{body}\nand the email address to send the URL is \n{recipient}";
            try
            {
                Process.Start(new ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
                Log.Commentary(details);
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred while trying to open your email client.\n" + details);
            }
        }

        /// <summary>
        /// Upload file to FileIo
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>file.io URL to download the file if successful or null</returns>
        public async Task<string> UploadFileToFileIo(string filePath)
        {
            using (var httpClient = new HttpClient())
            {
                using (var form = new MultipartFormDataContent())
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    var fileContent = new ByteArrayContent(fileBytes);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    form.Add(fileContent, "file", Path.GetFileName(filePath));

                    HttpResponseMessage response = await httpClient.PostAsync("https://file.io", form);
                    response.EnsureSuccessStatusCode();

                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(jsonResponse);

                    return json["link"]?.ToString() ?? null;
                }
            }
        }
    }
}