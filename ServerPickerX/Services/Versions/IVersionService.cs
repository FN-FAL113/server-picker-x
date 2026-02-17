using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace ServerPickerX.Services.Versions
{
    public interface IVersionService
    {
        Task CheckVersionAsync();
    }
}