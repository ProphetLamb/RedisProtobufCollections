using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace RedisProtobufCollections.Exceptions
{
    internal static class ThrowHelper
    {
        private static readonly Dictionary<ExceptionArgument, string> s_argumentNameMap = new();

        private static string GetArgumentName(ExceptionArgument argument)
        {
            if (s_argumentNameMap.TryGetValue(argument, out string? name))
                return name!;
            name = Enum.GetName(typeof(ExceptionArgument), argument);
            s_argumentNameMap.Add(argument, name!);
            return name!;
        }

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException_LessEqualZero(ExceptionArgument argument)
        {
            throw new ArgumentOutOfRangeException(GetArgumentName(argument), "Value must be greater then zero.");
        }

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException_LessZero(ExceptionArgument argument)
        {
            throw new ArgumentOutOfRangeException(GetArgumentName(argument), "Value must be greater then or equal to zero.");
        }

        [DoesNotReturn]
        internal static void ThrowArgumentOutOfRangeException_OverEqualsMax<T>(ExceptionArgument argument, in T maxExcl)
        {
            throw new ArgumentOutOfRangeException(GetArgumentName(argument), $"The value must be less then the maximum {maxExcl}");
        }

        [DoesNotReturn]
        internal static void ThrowArgumentException(ExceptionArgument argument, string? message)
        {
            throw new ArgumentException(message, GetArgumentName(argument));
        }

        internal static void ThrowIfObjectDisposed([DoesNotReturnIf(true)] bool disposed)
        {
            if (disposed)
                ThrowInvalidOperationException_ObjectDisposed();
        }

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException_ObjectDisposed()
        {
            throw new InvalidOperationException("The operation cannot be performed on an disposed object.");
        }

        internal static void ThrowIfObjectNotInitialized([DoesNotReturnIf(true)] bool notInitialized)
        {
            if (notInitialized)
                ThrowInvalidOperationException_ObjectNotInitialized();
        }

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException_ObjectNotInitialized()
        {
            throw new InvalidOperationException("The object is not initialized.");
        }

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException(string message)
        {
            throw new InvalidOperationException(message);
        }

        [DoesNotReturn]
        internal static void ThrowArgumentException_ArrayCapacityOverMax(ExceptionArgument argument, int reqiredCapacity)
        {
            throw new ArgumentException($"The array is of insufficient capacity to contains {reqiredCapacity} elements.", GetArgumentName(argument));
        }

        [DoesNotReturn]
        internal static void ThrowInvalidOperationException_EnumeratorUnsyncVersion()
        {
            throw new InvalidOperationException("The enumerator is unsynchronized from the collection. The version does not equals to collection version.");
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException()
        {
            throw new NotSupportedException();
        }
    }

    internal enum ExceptionArgument
    {
        count,
        initialCapacity,
        index,
        arrayIndex,
        array,
        optionAccessor
    }
}
