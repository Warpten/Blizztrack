using System.Diagnostics;

namespace Blizztrack.Shared.IO
{
    public class DelegateStream : Stream
    {
        private readonly Stream _innerStream;

        #region Properties

        public override bool CanRead => _innerStream.CanRead;

        public override bool CanSeek => _innerStream.CanSeek;

        public override bool CanWrite => _innerStream.CanWrite;

        public override long Length { get; }

        public override long Position
        {
            get { return _innerStream.Position; }
            set { _innerStream.Position = value; }
        }

        public override int ReadTimeout
        {
            get { return _innerStream.ReadTimeout; }
            set { _innerStream.ReadTimeout = value; }
        }

        public override bool CanTimeout => _innerStream.CanTimeout;

        public override int WriteTimeout
        {
            get { return _innerStream.WriteTimeout; }
            set { _innerStream.WriteTimeout = value; }
        }

        #endregion Properties

        public DelegateStream(Stream innerStream, long? length)
        {
            Debug.Assert(innerStream != null);
            _innerStream = innerStream;
            Length = innerStream.CanSeek ? innerStream.Length : (length ?? throw new InvalidOperationException());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync() => _innerStream.DisposeAsync();

        #region Read

        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);

        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _innerStream.Read(buffer);

        public override int ReadByte() => _innerStream.ReadByte();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _innerStream.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _innerStream.ReadAsync(buffer, cancellationToken);

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _innerStream.BeginRead(buffer, offset, count, callback, state);

        public override int EndRead(IAsyncResult asyncResult) => _innerStream.EndRead(asyncResult);

        public override void CopyTo(Stream destination, int bufferSize) => _innerStream.CopyTo(destination, bufferSize);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => _innerStream.CopyToAsync(destination, bufferSize, cancellationToken);

        #endregion Read

        #region Write

        public override void Flush() => _innerStream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);

        public override void SetLength(long value) => _innerStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => _innerStream.Write(buffer);

        public override void WriteByte(byte value) => _innerStream.WriteByte(value);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _innerStream.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _innerStream.WriteAsync(buffer, cancellationToken);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _innerStream.BeginWrite(buffer, offset, count, callback, state);

        public override void EndWrite(IAsyncResult asyncResult) => _innerStream.EndWrite(asyncResult);
        #endregion Write
    }
}
