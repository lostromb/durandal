namespace Durandal.Common.Logger
{
    using System.Collections.Generic;
    using System.Collections;
    using System;

    public class LoggingHistory : ILoggingHistory
    {
        private int _listSize = 0;
        private int _backlogSize;

        private LinkedListNode _first;
        private LinkedListNode _last;

        public LoggingHistory(int backlogSize = 1000)
        {
            _backlogSize = backlogSize;
        }

        public void Add(LogEvent value)
        {
            lock (this)
            {
                while (_listSize > _backlogSize)
                {
                    _first = _first.Next;
                    _first.Prev.Next = null;
                    _first.Prev = null;
                    _listSize -= 1;
                }
                LinkedListNode newNode = new LinkedListNode(value);
                if (_first == null)
                {
                    // Start a new list
                    _first = newNode;
                    _last = newNode;
                }
                else
                {
                    // Append to existing list
                    newNode.Prev = _last;
                    if (_last != null)
                    {
                        _last.Next = newNode;
                    }
                    _last = newNode;
                }
                _listSize += 1;
            }
        }

        public IEnumerable<LogEvent> FilterByCriteria(LogLevel level, bool iterateReverse = false)
        {
            return FilterByCriteria(new FilterCriteria() { Level = level }, iterateReverse);
        }

        public IEnumerable<LogEvent> FilterByCriteria(FilterCriteria criteria, bool iterateReverse = false)
        {
            lock (this)
            {
                if (iterateReverse)
                    return new EnumerableImpl(new EventEnumerator(_last, criteria, false));
                else
                    return new EnumerableImpl(new EventEnumerator(_first, criteria, true));
            }
        }

        IEnumerator<LogEvent> IEnumerable<LogEvent>.GetEnumerator()
        {
            lock (this)
            {
                return new EventEnumerator(_first);
            }
        }

        public IEnumerator GetEnumerator()
        {
            lock (this)
            {
                return new EventEnumerator(_first);
            }
        }

        /// <summary>
        /// A simple converter from IEnumerator to IEnumerable
        /// </summary>
        private class EnumerableImpl : IEnumerable<LogEvent>
        {
            private readonly IEnumerator<LogEvent> _enumerator;

            public EnumerableImpl(IEnumerator<LogEvent> enumerator)
            {
                _enumerator = enumerator;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _enumerator;
            }

            public IEnumerator<LogEvent> GetEnumerator()
            {
                return _enumerator;
            }
        }

        /// <summary>
        /// An enumerator which goes forward or backwards over log history
        /// </summary>
        private class EventEnumerator : IEnumerator<LogEvent>
        {
            private readonly LinkedListNode _firstNode;
            private readonly FilterCriteria _filter;
            private readonly bool _forward;
            private LinkedListNode _curNode;
            private bool _finished;


            public EventEnumerator(LinkedListNode firstNode, FilterCriteria criteria = null, bool forward = true)
            {
                _curNode = null;
                _firstNode = firstNode;
                _filter = criteria;
                _forward = forward;
                _finished = _firstNode == null;
            }
            
            public LogEvent Current
            {
                get
                {
                    if (_curNode == null)
                    {
                        return null;
                    }

                    return _curNode.Event;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (_curNode == null)
                    {
                        return null;
                    }

                    return _curNode.Event;
                }
            }

            public void Reset()
            {
                _curNode = null;
                _finished = _firstNode == null;
            }

            public bool MoveNext()
            {
                if (_finished || _firstNode == null)
                {
                    return false;
                }

                if (_curNode == null && !_finished)
                {
                    // Iterate to first node
                    _curNode = _firstNode;
                    if (_filter == null || _filter.PassesFilter(_curNode.Event))
                    {
                        return true;
                    }
                }

                do
                {
                    if (_forward)
                    {
                        _curNode = _curNode.Next;
                    }
                    else
                    {
                        _curNode = _curNode.Prev;
                    }

                    if (_curNode == null)
                    {
                        _finished = true;
                        return false;
                    }
                } while (_filter != null && !_filter.PassesFilter(_curNode.Event));

                return true;
            }

            public void Dispose() { }
        }

        /// <summary>
        /// An element of the log history linked list
        /// </summary>
        private class LinkedListNode
        {
            public readonly LogEvent Event;
            public volatile LinkedListNode Next = null;
            public volatile LinkedListNode Prev = null;

            public LinkedListNode(LogEvent value)
            {
                Event = value;
            }
        }
    }
}
