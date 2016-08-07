using System.Collections.Generic;

namespace FireSharp.EventStreaming
{
    internal class SimpleCacheItem
    {
        private List<SimpleCacheItem> _children;
        public string Name { get; set; }
        public string Value { get; set; }
        public SimpleCacheItem Parent { get; set; }

        public List<SimpleCacheItem> Children => _children ?? (_children = new List<SimpleCacheItem>());

        public bool ContainsChildren(string name)
        {
            foreach (SimpleCacheItem item in _children)
            {
                if (item.Name.Equals(name))
                {
                    return true;
                }
            }

            return false;
        }
    }
}