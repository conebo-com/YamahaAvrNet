using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YamahaAvrNet.Models;

namespace YamahaAvrNet.Features.SpeakerSetup
{
    /// <summary>
    /// Phase-one contract only. No protocol is selected and NO hardware command is sent.
    /// Writes must later be gated by per-model/per-feature verified capabilities.
    /// </summary>
    internal sealed class YamahaSpeakerSetupController : IYamahaSpeakerSetup
    {
        private readonly Action _ensureNotDisposed;

        internal YamahaSpeakerSetupController(Action ensureNotDisposed)
        {
            _ensureNotDisposed = ensureNotDisposed ??
                throw new ArgumentNullException(nameof(ensureNotDisposed));
        }

        public Task<int?> GetActivePatternAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            _ensureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            throw Unavailable();
        }

        public Task<IReadOnlyList<YamahaSpeakerPatternInfo>> GetPatternsAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _ensureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            throw Unavailable();
        }

        public Task<IReadOnlyDictionary<string, YamahaSpeakerValue>> GetLevelsAsync(
            int pattern, CancellationToken cancellationToken = default(CancellationToken))
        {
            _ensureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern < 1)
                throw new ArgumentOutOfRangeException(nameof(pattern));
            throw Unavailable();
        }

        public Task<IReadOnlyDictionary<string, YamahaSpeakerValue>> GetDistancesAsync(
            int pattern, CancellationToken cancellationToken = default(CancellationToken))
        {
            _ensureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern < 1)
                throw new ArgumentOutOfRangeException(nameof(pattern));
            throw Unavailable();
        }

        public Task SetLevelAsync(int pattern, string nativeId, decimal decibels,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _ensureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChannel(pattern, nativeId);
            throw Unavailable();
        }

        public Task SetDistanceAsync(int pattern, string nativeId, decimal meters,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _ensureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            ValidateChannel(pattern, nativeId);
            throw Unavailable();
        }

        private static void ValidateChannel(int pattern, string nativeId)
        {
            if (pattern < 1)
                throw new ArgumentOutOfRangeException(nameof(pattern));
            if (string.IsNullOrWhiteSpace(nativeId))
                throw new ArgumentException("Native channel ID is required.", nameof(nativeId));
        }

        private static NotSupportedException Unavailable()
        {
            return new NotSupportedException(
                "Speaker communication is not implemented in phase 1. No command was sent.");
        }
    }
}
