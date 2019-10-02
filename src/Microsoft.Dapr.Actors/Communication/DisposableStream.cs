// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Communication
{
    using System.IO;

    /// <summary>
    ///     Wraps an implementation of Stream and provides an idempotent impplementation of Dispose.
    /// </summary>
    internal class DisposableStream : Stream
    {
        private readonly Stream streamImplementation;
        private bool disposed;

        public DisposableStream(Stream streamImplementation)
        {
            this.streamImplementation = streamImplementation;
        }

        public override bool CanRead
        {
            get { return this.streamImplementation.CanRead; }
        }

        public override bool CanSeek
        {
            get { return this.streamImplementation.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return this.streamImplementation.CanWrite; }
        }

        public override long Length
        {
            get { return this.streamImplementation.Length; }
        }

        public override long Position
        {
            get { return this.streamImplementation.Position; }
            set { this.streamImplementation.Position = value; }
        }

        public override void Flush()
        {
            this.streamImplementation.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.streamImplementation.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this.streamImplementation.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.streamImplementation.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.streamImplementation.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    this.streamImplementation.Dispose();
                }
            }

            this.disposed = true;
        }
    }
}