using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace YamahaAvrNet.Models
{
    /// <summary>An observation about an optional legacy description endpoint.</summary>
    public enum YamahaDescriptorAttemptStatus
    {
        Found = 0,
        NotFound = 1,
        HttpError = 2,
        InvalidXml = 3,
        UnrecognizedDescription = 4,
        Timeout = 5,
        NetworkError = 6,
        InvalidContent = 7
    }

    public sealed class YamahaDescriptorAttempt
    {
        internal YamahaDescriptorAttempt(string path, YamahaDescriptorAttemptStatus status,
            int? httpStatusCode, string detail)
        {
            Path = path;
            Status = status;
            HttpStatusCode = httpStatusCode;
            Detail = detail;
        }
        public string Path { get; private set; }
        public YamahaDescriptorAttemptStatus Status { get; private set; }
        public int? HttpStatusCode { get; private set; }
        public string Detail { get; private set; }
    }

    /// <summary>Optional descriptor diagnostics do not indicate protocol support.</summary>
    public sealed class YamahaLegacyDescriptorDiagnostic
    {
        internal YamahaLegacyDescriptorDiagnostic(IEnumerable<YamahaDescriptorAttempt> attempts,
            DateTimeOffset checkedUtc)
        {
            Attempts = new ReadOnlyCollection<YamahaDescriptorAttempt>(
                new List<YamahaDescriptorAttempt>(attempts));
            CheckedUtc = checkedUtc;
        }
        public IReadOnlyList<YamahaDescriptorAttempt> Attempts { get; private set; }
        public DateTimeOffset CheckedUtc { get; private set; }
    }
}
