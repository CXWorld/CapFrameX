using CapFrameX.Statistics.NetStandard;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    [TestClass]
    public class CircularBufferTest
    {
        #region Constructor Tests

        [TestMethod]
        public void Constructor_ValidCapacity_CreatesBuffer()
        {
            var buffer = new CircularBuffer<double>(100);
            Assert.AreEqual(100, buffer.Capacity);
            Assert.AreEqual(0, buffer.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_ZeroCapacity_ThrowsException()
        {
            var buffer = new CircularBuffer<double>(0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor_NegativeCapacity_ThrowsException()
        {
            var buffer = new CircularBuffer<double>(-1);
        }

        #endregion

        #region Add Tests

        [TestMethod]
        public void Add_SingleElement_IncreasesCount()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);

            Assert.AreEqual(1, buffer.Count);
            Assert.IsFalse(buffer.IsEmpty);
        }

        [TestMethod]
        public void Add_MultipleElements_MaintainsOrder()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            Assert.AreEqual(1.0, buffer[0]);
            Assert.AreEqual(2.0, buffer[1]);
            Assert.AreEqual(3.0, buffer[2]);
        }

        [TestMethod]
        public void Add_OverCapacity_OverwritesOldest()
        {
            var buffer = new CircularBuffer<double>(3);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);
            buffer.Add(4.0); // Should overwrite 1.0

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(2.0, buffer[0]); // Oldest is now 2.0
            Assert.AreEqual(3.0, buffer[1]);
            Assert.AreEqual(4.0, buffer[2]); // Newest is 4.0
        }

        [TestMethod]
        public void Add_WrapAround_MaintainsCorrectOrder()
        {
            var buffer = new CircularBuffer<double>(3);

            // Fill buffer
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            // Wrap around multiple times
            buffer.Add(4.0);
            buffer.Add(5.0);
            buffer.Add(6.0);
            buffer.Add(7.0);

            Assert.AreEqual(3, buffer.Count);
            Assert.AreEqual(5.0, buffer[0]);
            Assert.AreEqual(6.0, buffer[1]);
            Assert.AreEqual(7.0, buffer[2]);
        }

        #endregion

        #region Peek Tests

        [TestMethod]
        public void PeekFirst_ReturnsOldestElement()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            Assert.AreEqual(1.0, buffer.PeekFirst());
        }

        [TestMethod]
        public void PeekLast_ReturnsNewestElement()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            Assert.AreEqual(3.0, buffer.PeekLast());
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void PeekFirst_EmptyBuffer_ThrowsException()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.PeekFirst();
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void PeekLast_EmptyBuffer_ThrowsException()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.PeekLast();
        }

        #endregion

        #region RemoveFirst Tests

        [TestMethod]
        public void RemoveFirst_ReturnsAndRemovesOldest()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            var removed = buffer.RemoveFirst();

            Assert.AreEqual(1.0, removed);
            Assert.AreEqual(2, buffer.Count);
            Assert.AreEqual(2.0, buffer.PeekFirst());
        }

        [TestMethod]
        public void RemoveFirst_AllElements_EmptiesBuffer()
        {
            var buffer = new CircularBuffer<double>(3);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            buffer.RemoveFirst();
            buffer.RemoveFirst();
            buffer.RemoveFirst();

            Assert.IsTrue(buffer.IsEmpty);
            Assert.AreEqual(0, buffer.Count);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RemoveFirst_EmptyBuffer_ThrowsException()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.RemoveFirst();
        }

        #endregion

        #region Clear Tests

        [TestMethod]
        public void Clear_EmptiesBuffer()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            buffer.Clear();

            Assert.IsTrue(buffer.IsEmpty);
            Assert.AreEqual(0, buffer.Count);
        }

        [TestMethod]
        public void Clear_AllowsReuse()
        {
            var buffer = new CircularBuffer<double>(3);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            buffer.Clear();

            buffer.Add(10.0);
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(10.0, buffer[0]);
        }

        #endregion

        #region ToArray Tests

        [TestMethod]
        public void ToArray_ReturnsCorrectElements()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            var array = buffer.ToArray();

            Assert.AreEqual(3, array.Length);
            Assert.AreEqual(1.0, array[0]);
            Assert.AreEqual(2.0, array[1]);
            Assert.AreEqual(3.0, array[2]);
        }

        [TestMethod]
        public void ToArray_AfterWrapAround_ReturnsCorrectOrder()
        {
            var buffer = new CircularBuffer<double>(3);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);
            buffer.Add(4.0);
            buffer.Add(5.0);

            var array = buffer.ToArray();

            Assert.AreEqual(3, array.Length);
            Assert.AreEqual(3.0, array[0]);
            Assert.AreEqual(4.0, array[1]);
            Assert.AreEqual(5.0, array[2]);
        }

        [TestMethod]
        public void ToArray_EmptyBuffer_ReturnsEmptyArray()
        {
            var buffer = new CircularBuffer<double>(10);
            var array = buffer.ToArray();

            Assert.AreEqual(0, array.Length);
        }

        [TestMethod]
        public void ToArray_ReuseDestination_UsesProvidedBuffer()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            var destination = new double[10];
            var result = buffer.ToArray(destination);

            Assert.AreSame(destination, result);
            Assert.AreEqual(1.0, result[0]);
            Assert.AreEqual(2.0, result[1]);
            Assert.AreEqual(3.0, result[2]);
        }

        #endregion

        #region ToList Tests

        [TestMethod]
        public void ToList_ReturnsCorrectElements()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            var list = buffer.ToList();

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(1.0, list[0]);
            Assert.AreEqual(2.0, list[1]);
            Assert.AreEqual(3.0, list[2]);
        }

        [TestMethod]
        public void ToList_ReuseDestination_ReusesProvidedList()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            var destination = new List<double>(20);
            var result = buffer.ToList(destination);

            Assert.AreSame(destination, result);
            Assert.AreEqual(3, result.Count);
        }

        #endregion

        #region Indexer Tests

        [TestMethod]
        public void Indexer_ValidIndex_ReturnsElement()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            Assert.AreEqual(2.0, buffer[1]);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Indexer_NegativeIndex_ThrowsException()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            var value = buffer[-1];
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Indexer_IndexOutOfRange_ThrowsException()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            var value = buffer[5];
        }

        #endregion

        #region Enumeration Tests

        [TestMethod]
        public void GetEnumerator_IteratesInOrder()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            var values = buffer.ToList();

            Assert.AreEqual(3, values.Count);
            Assert.AreEqual(1.0, values[0]);
            Assert.AreEqual(2.0, values[1]);
            Assert.AreEqual(3.0, values[2]);
        }

        [TestMethod]
        public void GetEnumerator_AfterWrapAround_IteratesCorrectly()
        {
            var buffer = new CircularBuffer<double>(3);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);
            buffer.Add(4.0);

            var values = new List<double>();
            foreach (var item in buffer)
            {
                values.Add(item);
            }

            Assert.AreEqual(3, values.Count);
            Assert.AreEqual(2.0, values[0]);
            Assert.AreEqual(3.0, values[1]);
            Assert.AreEqual(4.0, values[2]);
        }

        #endregion

        #region IsFull Tests

        [TestMethod]
        public void IsFull_NotFull_ReturnsFalse()
        {
            var buffer = new CircularBuffer<double>(10);
            buffer.Add(1.0);

            Assert.IsFalse(buffer.IsFull);
        }

        [TestMethod]
        public void IsFull_AtCapacity_ReturnsTrue()
        {
            var buffer = new CircularBuffer<double>(3);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);

            Assert.IsTrue(buffer.IsFull);
        }

        [TestMethod]
        public void IsFull_AfterOverwrite_StaysTrue()
        {
            var buffer = new CircularBuffer<double>(3);
            buffer.Add(1.0);
            buffer.Add(2.0);
            buffer.Add(3.0);
            buffer.Add(4.0);

            Assert.IsTrue(buffer.IsFull);
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public void Add_LargeNumberOfElements_Completes()
        {
            var buffer = new CircularBuffer<double>(10000);

            for (int i = 0; i < 100000; i++)
            {
                buffer.Add(i);
            }

            // Should only contain last 10000 elements
            Assert.AreEqual(10000, buffer.Count);
            Assert.AreEqual(90000, buffer.PeekFirst());
            Assert.AreEqual(99999, buffer.PeekLast());
        }

        [TestMethod]
        public void RemoveFirst_LargeNumberOfOperations_Completes()
        {
            var buffer = new CircularBuffer<double>(1000);

            // Add and remove many times
            for (int i = 0; i < 10000; i++)
            {
                buffer.Add(i);
                if (buffer.Count > 500)
                {
                    buffer.RemoveFirst();
                }
            }

            Assert.IsTrue(buffer.Count <= 500);
        }

        #endregion
    }
}
