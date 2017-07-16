﻿using System.Collections.Generic;

namespace CSharpUtils.Arrays
{
	public class ArrayWrapper<TType> : IArray<TType>
	{
		TType[] Array;

		public ArrayWrapper()
		{
			this.Array = new TType[0];
		}

		public ArrayWrapper(TType[] Array)
		{
			this.Array = Array;
		}

		public TType this[int Index]
		{
			get
			{
				return Array[Index];
			}
			set
			{
				Array[Index] = value;
			}
		}

		public static implicit operator TType[](ArrayWrapper<TType> ArrayWrapper)
		{
			return ArrayWrapper.Array; 
		}

		public static implicit operator ArrayWrapper<TType>(TType[] Array)
		{
			return new ArrayWrapper<TType>(Array);
		}

		public int Length
		{
			get { return Array.Length; }
		}


		public TType[] GetArray()
		{
			return Array;
		}

		public IEnumerator<TType> GetEnumerator()
		{
			for (int n = 0; n < Length; n++) yield return this[n];
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			for (int n = 0; n < Length; n++) yield return this[n];
		}
	}
}
