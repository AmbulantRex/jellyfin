using System;

namespace MediaBrowser.Common.Extensions
{
    /// <summary>
    /// Class DirectoryTraversalException.
    /// </summary>
    public class DirectoryTraversalException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryTraversalException" /> class.
        /// </summary>
        public DirectoryTraversalException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectoryTraversalException" /> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public DirectoryTraversalException(string message)
            : base(message)
        {
        }
    }
}
