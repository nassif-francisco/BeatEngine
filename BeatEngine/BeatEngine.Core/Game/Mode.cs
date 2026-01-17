using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatEngine.Core.Game
{
    public class Mode
    {
        public string Tag { get; set; }

        public string NextModeTag { get; set; }

        public string ToNextMode()
        {
            return NextModeTag;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class DrawInModeAttribute : Attribute
    {
        public string Key { get; }

        public DrawInModeAttribute(string key)
        {
            Key = key;
        }
    }
}
