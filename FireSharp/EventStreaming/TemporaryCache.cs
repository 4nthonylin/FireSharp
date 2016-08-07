using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FireSharp.EventStreaming
{
    internal sealed class TemporaryCache : IDisposable
    {
        private readonly LinkedList<SimpleCacheItem> _pathFromRootList = new LinkedList<SimpleCacheItem>();
        private readonly char[] _seperator = {'/'};
        private readonly object _treeLock = new object();

        public object Context = null;

        public TemporaryCache()
        {
            Root.Name = string.Empty;
            Root.Parent = null;
            Root.Name = null;
        }

        ~TemporaryCache()
        {
            Dispose(false);
        }

        internal SimpleCacheItem Root { get; } = new SimpleCacheItem();

        public void Update(string path, JToken data, bool replace)
        {
            lock (_treeLock)
            {
                var root = FindRoot(path);

                if (replace)
                {
                    DeleteChild(root);

                    root.Parent?.Children.Add(root);
                }
                foreach (JProperty prop in data.Children<JProperty>())
                {
                    using (var reader = new JsonTextReader(new StringReader(data.ToString())))
                    {
                        if (root.ContainsChildren(prop.Name))
                        {

                        }
                        else
                        {
                            UpdateChildren(root, reader);
                            OnAdded(new ValueAddedEventArgs(PathFromRoot(root), prop));
                        }
                    }
                }
            }
        }

        private SimpleCacheItem FindRoot(string path)
        {
            var segments = path.Split(_seperator, StringSplitOptions.RemoveEmptyEntries);

            return segments.Aggregate(Root, GetNamedChild);
        }

        private static SimpleCacheItem GetNamedChild(SimpleCacheItem root, string segment)
        {
            var newRoot = root.Children.FirstOrDefault(c => c.Name == segment);

            if (newRoot == null)
            {
                newRoot = new SimpleCacheItem {Name = segment, Parent = root};
                root.Children.Add(newRoot);
            }

            return newRoot;
        }

        private void UpdateChildren(SimpleCacheItem root, JsonTextReader reader)
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        UpdateChildren(GetNamedChild(root, reader.Value.ToString()), reader);
                        break;
                    case JsonToken.Boolean:
                    case JsonToken.Bytes:
                    case JsonToken.Date:
                    case JsonToken.Float:
                    case JsonToken.Integer:
                    case JsonToken.String:
                        if (string.IsNullOrEmpty(root.Value))
                        {
                            root.Value = reader.Value.ToString();
                        }
                        else
                        {
                            var oldData = root.Value;
                            root.Value = reader.Value.ToString();
                            //OnUpdated(new ValueChangedEventArgs(PathFromRoot(root), root.Value, oldData));
                        }

                        return;
                    case JsonToken.Null:
                        DeleteChild(root);
                        return;
                    case JsonToken.EndObject:
                        return;
                }
            }
        }

        private void DeleteChild(SimpleCacheItem root)
        {
            if (root.Parent != null)
            {
                if (RemoveChildFromParent(root))
                {
                    OnRemoved(new ValueRemovedEventArgs(PathFromRoot(root)));
                }
            }
            else
            {
                foreach (var child in root.Children.ToArray())
                {
                    RemoveChildFromParent(child);
                    OnRemoved(new ValueRemovedEventArgs(PathFromRoot(child)));
                }
            }
        }

        private bool RemoveChildFromParent(SimpleCacheItem child)
        {
            if (child.Parent != null)
            {
                return child.Parent.Children.Remove(child);
            }

            return false;
        }

        private string PathFromRoot(SimpleCacheItem root)
        {
            var size = 1;

            while (root.Name != null)
            {
                size += root.Name.Length + 1;
                _pathFromRootList.AddFirst(root);
                root = root.Parent;
            }

            if (_pathFromRootList.Count == 0)
            {
                return "/";
            }

            var sb = new StringBuilder(size);
            foreach (var d in _pathFromRootList)
            {
                sb.Append($"/{d.Name}");
            }

            _pathFromRootList.Clear();

            return sb.ToString();
        }

        private void OnAdded(ValueAddedEventArgs args)
        {
            Added?.Invoke(this, args, Context);
        }

        private void OnUpdated(ValueChangedEventArgs args)
        {
            Changed?.Invoke(this, args, Context);
        }

        private void OnRemoved(ValueRemovedEventArgs args)
        {
            Removed?.Invoke(this, args, Context);
        }

        public event ValueAddedEventHandler Added;
        public event ValueChangedEventHandler Changed;
        public event ValueRemovedEventHandler Removed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Added = null;
                Changed = null;
                Removed = null;
            }
        }
    }
}