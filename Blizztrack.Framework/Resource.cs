using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace Blizztrack.Framework
{
    public interface IResource
    {
        public ResourceType Type { get; }
        public long Offset { get; }
        public long Length { get; }
    }
}
