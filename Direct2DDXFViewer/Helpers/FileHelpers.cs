using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Direct2DDXFViewer.Helpers
{
    public static class FileHelpers
    {
        public static bool IsFileInUse(string filePath)
        {
            try
            {
                using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // If we can open the file with exclusive access, it's not in use
                    return false;
                }
            }
            catch (IOException)
            {
                // If an IOException is thrown, the file is in use
                return true;
            }
        }
    }
}
