using System.Diagnostics;
using System.IO;

namespace Sources.Helpers
{
    public static class FileHelper
    {
        public static void OpenFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            try
            {
                Process.Start(new ProcessStartInfo(filePath)
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                // فشل فتح الملف ليس حرجاً، يمكن للمستخدم فتحه يدوياً
            }
        }
    }
}
