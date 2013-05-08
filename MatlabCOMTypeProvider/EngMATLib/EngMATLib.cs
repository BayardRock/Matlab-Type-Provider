// Matlab Interface Library
// by Emanuele Ruffaldi 2002
// http://www.sssup.it/~pit/
// mailto:pit@sssup.it
//
// Description: MATLAB Engine API interface
using System;
using System.Text;
using System.Runtime.InteropServices;

namespace EngMATLib
{

    /// <summary>
    /// A Class to Interface MATLAB with C# using MATLAB Engine API
    /// Of the three methods (COM,DDE,MAT) this is the faster and better one
    /// although it uses unsafe methods to gain direct memory access
    /// </summary>
    public class EngMATAccess : IDisposable
    {
        /// <summary>
        /// Creates the connection with a default buffer
        /// </summary>
        public EngMATAccess()
            : this(128)
        {
        }

        /// <summary>
        /// Creates the connection
        /// </summary>
        public EngMATAccess(int buffersize)
        {
            engine = MATInvoke.engOpen(null);
            buffer = IntPtr.Zero;
            resultBuffer = false;
            BufferSize = buffersize;
            BufferingActive = true;
        }

        protected virtual void Dispose(bool disp)
        {
            if (Active)
            {
                MATInvoke.engClose(engine);
                Marshal.FreeHGlobal(buffer);
                buffer = IntPtr.Zero;
                engine = IntPtr.Zero;
            }
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~EngMATAccess()
        {
            Dispose(false);
        }

        /// <summary>
        /// Tells if it's active
        /// </summary>
        public bool Active
        {
            get { return engine != IntPtr.Zero; }
        }

        /// <summary>
        /// Evaluates an expression and returns true on completion		
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public bool Evaluate(string expression)
        {
            return MATInvoke.engEvalString(engine, expression) == 0;
        }

        /// <summary>
        /// Evaluates an expression and returns true on completion		
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public string EvaluateAsString(string expression)
        {
            if (!BufferingActive)
                BufferingActive = true;
            if (MATInvoke.engEvalString(engine, expression) == 0)
                return LastResult;
            else
                return null;
        }

        /// <summary>
        /// Say if the MATLAB window is visible
        /// </summary>
        /// <returns></returns>
        public bool IsVisible()
        {
            return MATInvoke.engIsVisible(engine);
        }

        /// <summary>
        /// Fixes the MATLAB windows visibility
        /// </summary>
        /// <param name="b"></param>
        public void SetVisible(bool b)
        {
            MATInvoke.engSetVisible(engine, b);
        }

        /// <summary>
        /// Gets a matrix variable 
        /// </summary>
        /// <param name="name">name of the matrix</param>
        /// <param name="data">the matrix, preallocated or not</param>
        /// <returns></returns>
        public bool GetMatrix(string name, ref double[,] data)
        {
            IntPtr aa = MATInvoke.engGetArray(engine, name);
            if (aa == IntPtr.Zero)
                return false;
            else
            {
                bool r = MxArrayToMatrix(aa, ref data);
                MATInvoke.mxDestroyArray(aa);
                return r;
            }
        }

        /// <summary>
        /// Gets a matrix variable 
        /// </summary>
        /// <param name="name">name of the matrix</param>
        /// <param name="data">the matrix, preallocated or not</param>
        /// <returns></returns>
        public Matrix GetMatrix(string name)
        {
            IntPtr aa = MATInvoke.engGetArray(engine, name);
            if (aa == IntPtr.Zero)
                return null;
            else
            {
                return new Matrix(aa);
            }
        }

        /// <summary>
        /// Sets a matrix variable
        /// </summary>
        /// <param name="name">Name</param>
        /// <param name="data">Data</param>
        public bool SetMatrix(string name, double[,] data)
        {
            if (data == null || !Active) return false;

            IntPtr mta = MatrixToMxArray(data, false);
            if (mta == IntPtr.Zero)
                return false;
            MATInvoke.mxSetName(mta, name);
            bool b = MATInvoke.engPutArray(engine, mta) == 0;
            MATInvoke.mxDestroyArray(mta);
            return b;
        }

        /// <summary>
        /// Sets a matrix variable
        /// </summary>
        /// <param name="name">Name</param>
        /// <param name="data">Data</param>
        public bool SetMatrix(string name, Matrix m)
        {
            if (m != null && !Active) return false;

            IntPtr mta = m.Mx; ;
            if (mta == IntPtr.Zero)
                return false;
            MATInvoke.mxSetName(mta, name);
            bool b = MATInvoke.engPutArray(engine, mta) == 0;
            return b;
        }

        /// <summary>
        /// Closes the Connection to MATLAB
        /// </summary>
        public void Close()
        {
            if (Active)
                Dispose();
        }

        public static unsafe IntPtr MatrixToMxArray(double[,] fromArray, bool bycol)
        {
            int rows = fromArray.GetLength(0);
            int cols = fromArray.GetLength(1);
            IntPtr mta = MATInvoke.mxCreateDoubleMatrix(rows, cols, MATInvoke.mxComplexity.mxREAL);
            if (mta != IntPtr.Zero)
            {
                double* p = (double*)MATInvoke.mxGetPr(mta).ToPointer();

                if (!bycol)
                {
                    // MATLAB stores data by column
                    for (int j = 0; j < cols; j++)
                        for (int i = 0; i < rows; i++)
                            *p++ = fromArray[i, j];
                }
                else
                {
                    for (int j = 0; j < cols; j++)
                        for (int i = 0; i < rows; i++)
                            *p++ = fromArray[i, j];
                }
            }
            return mta;
        }

        public static IntPtr MatrixToMxArray(double[] fromArray, int cols)
        {
            if (cols == 0)
                cols = 1;
            int rows = fromArray.Length / cols;
            IntPtr mta = MATInvoke.mxCreateDoubleMatrix(rows, cols, MATInvoke.mxComplexity.mxREAL);
            if (mta != IntPtr.Zero)
            {
                Marshal.Copy(fromArray, 0, MATInvoke.mxGetPr(mta), fromArray.Length);
            }
            return mta;
        }

        public static unsafe bool MxArrayToMatrix(Matrix mat, ref double[,] toArray)
        {
            return MxArrayToMatrix(mat.Mx, ref toArray);
        }

        internal static unsafe bool MxArrayToMatrix(IntPtr fromArray, ref double[,] toArray)
        {
            int rows = MATInvoke.mxGetM(fromArray);
            int cols = MATInvoke.mxGetN(fromArray);

            MATInvoke.mxClassID type = MATInvoke.mxGetClassID(fromArray);

            // Type Check
            if (type == MATInvoke.mxClassID.DOUBLE)
            {
                // Size Check
                if (toArray == null || toArray.GetLength(0) != rows || toArray.GetLength(1) != cols)
                    toArray = new double[rows, cols];

                // Copy 
                double* p = (double*)MATInvoke.mxGetData(fromArray).ToPointer();

                // MATLAB stores data by column			
                for (int j = 0; j < cols; j++)
                    for (int i = 0; i < rows; i++)
                        toArray[i, j] = *p++;
                return true;
            }
            else
                return false;
        }

        public static bool MxArrayToMatrix(Matrix mat, ref double[] toArray, out int cols)
        {
            return MxArrayToMatrix(mat.Mx, ref toArray, out cols);
        }

        internal static bool MxArrayToMatrix(IntPtr fromArray, ref double[] toArray, out int cols)
        {
            int rows = MATInvoke.mxGetM(fromArray);
            cols = MATInvoke.mxGetN(fromArray);
            MATInvoke.mxClassID type = MATInvoke.mxGetClassID(fromArray);

            // Type Check
            if (type == MATInvoke.mxClassID.DOUBLE)
            {
                if (toArray == null || toArray.Length < rows * cols)
                    toArray = new double[rows * cols];

                Marshal.Copy(MATInvoke.mxGetData(fromArray), toArray, 0, rows * cols);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// The buffering status
        /// </summary>
        public bool BufferingActive
        {
            get { return resultBuffer; }
            set
            {
                if (resultBuffer != value)
                {
                    resultBuffer = value;
                    if (buffer == IntPtr.Zero)
                        buffer = Marshal.AllocHGlobal(bufferSize);
                    if (value)
                    {
                        MATInvoke.engOutputBuffer(engine, buffer, bufferSize);
                    }
                    else
                        MATInvoke.engOutputBuffer(engine, IntPtr.Zero, 0);
                }

            }

        }

        /// <summary>
        /// Returns the string result of the last execution
        /// </summary>
        public string LastResult
        {
            get { return Marshal.PtrToStringAnsi(buffer); }
        }

        /// <summary>
        /// Gets the size of the result buffer
        /// </summary>
        public int BufferSize
        {
            get { return bufferSize; }
            set
            {
                if (bufferSize == value && buffer != IntPtr.Zero) return;
                Marshal.FreeHGlobal(buffer);
                buffer = Marshal.AllocHGlobal(value);
                bufferSize = value;
                if (BufferingActive)
                    MATInvoke.engOutputBuffer(engine, buffer, bufferSize);
                else
                    MATInvoke.engOutputBuffer(engine, IntPtr.Zero, 0);
            }
        }


        /// <summary>
        /// The MATLAB Engine Handle
        /// </summary>
        IntPtr engine;
        bool resultBuffer;
        int bufferSize;
        IntPtr buffer;
    }

    /// <summary>
    /// A wrapper for mxArray
    /// </summary>
    public class Matrix : IDisposable
    {
        /// <summary>
        /// Destruct the mxArray
        /// </summary>
        /// <param name="disp"></param>
        protected virtual void Dispose(bool disp)
        {
            if (max != IntPtr.Zero)
            {
                MATInvoke.mxDestroyArray(max);
                max = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Creates an empty Matrix rows x columns
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="cols"></param>
        public Matrix(int rows, int cols)
        {
            max = MATInvoke.mxCreateDoubleMatrix(rows, cols, MATInvoke.mxComplexity.mxREAL);
        }

        /// <summary>
        /// By Rows Matrix Constructor
        /// </summary>
        /// <param name="data"></param>
        public Matrix(double[,] data)
            : this(data, true)
        {
        }

        /// <summary>
        /// Creates a 3 dimensional matrix, for Image Processing
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="cols"></param>
        /// <param name="planes"></param>
        /// <param name="t"></param>
        public Matrix(int rows, int cols, int planes, Type t)
        {
            int[] dims = { rows, cols, planes };
            MATInvoke.mxClassID tt = MATInvoke.Type2ClassID(t);
            max = MATInvoke.mxCreateNumericArray(3, dims, tt, MATInvoke.mxComplexity.mxREAL);
        }

        /// <summary>
        /// Creates a Matrix with the specific type
        /// </summary>
        /// <param name="rows"></param>
        /// <param name="cols"></param>
        /// <param name="t"></param>
        public Matrix(int rows, int cols, Type t)
        {
            MATInvoke.mxClassID tt = MATInvoke.Type2ClassID(t);
            max = MATInvoke.mxCreateNumericMatrix(rows, cols, tt, MATInvoke.mxComplexity.mxREAL);
        }

        /// <summary>
        /// Constructor from a matrix of data, the bycol flags specifies to consider
        /// data by column (MATLAB) or by rows
        /// </summary>
        /// <param name="data"></param>
        /// <param name="bycol"></param>
        public Matrix(double[,] data, bool bycol)
        {
            max = EngMATAccess.MatrixToMxArray(data, bycol);
        }

        /// <summary>
        /// Construct a Matrix from a mxArray IntPtre, use with care!
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public static implicit operator IntPtr(Matrix m)
        {
            return m.max;
        }

        /// <summary>
        /// Special constructor used to create a Matrix directly from a valure
        /// returned by custom DLLs
        /// </summary>
        /// <param name="fromMxArray"></param>
        public Matrix(IntPtr fromMxArray)
        {
            max = fromMxArray;
        }

        /// <summary>
        /// The number of columns of the matrix
        /// </summary>
        public Matrix(double[] data, int cols)
        {
            max = EngMATAccess.MatrixToMxArray(data, cols);
        }

        /// <summary>
        /// The number of rows of the matrix
        /// </summary>
        public int Rows
        {
            get { return MATInvoke.mxGetM(max); }
        }

        /// <summary>
        /// The number of columns of the matrix
        /// </summary>
        public int Cols
        {
            get { return MATInvoke.mxGetN(max); }
        }

        /// <summary>
        /// IDisposable implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Returns the mxArray
        /// </summary>
        /// <returns></returns>
        public double[,] AsMatrix()
        {
            double[,] r = null;
            EngMATAccess.MxArrayToMatrix(max, ref r);
            return r;
        }

        /// <summary>
        /// Returns as a Vector
        /// </summary>
        /// <returns></returns>
        public double[] AsVector()
        {
            double[] r = null;
            int cols;
            EngMATAccess.MxArrayToMatrix(max, ref r, out cols);
            return r;
        }

        /// <summary>
        /// Read accessor
        /// </summary>
        public double this[int r, int c]
        {
            get
            {
                // Copy 
                double v;
                unsafe
                {
                    double* p = (double*)MATInvoke.mxGetData(max).ToPointer();
                    v = p[Cols * r + c];
                }
                return v;
            }
        }


        /// <summary>
        /// Returns the internal pointer to mxArray
        /// </summary>
        internal IntPtr Mx
        {
            get { return max; }
        }

        /// <summary>
        /// Destroy the associated matrix
        /// </summary>
        ~Matrix()
        {
            Dispose(false);
        }

        IntPtr max;
    }

    /// <summary>
    /// A Structure that describes a Matrix
    /// </summary>
    public class MatrixDescription
    {
        /// <summary>
        /// Create it from an mxArray
        /// </summary>
        /// <param name="ma"></param>
        internal MatrixDescription(IntPtr ma)
        {
            if (ma == IntPtr.Zero)
                return;
            Rows = MATInvoke.mxGetM(ma);
            Cols = MATInvoke.mxGetN(ma);
            Type = MATInvoke.mxGetClassID(ma);
            Name = MATInvoke.mxGetName(ma);
        }

        /// <summary>
        /// The string representation of the data elements
        /// </summary>
        public string TypeName
        {
            get { return Enum.GetName(typeof(MATInvoke.mxClassID), Type); }
        }

        /// <summary>
        /// A string representation of the description
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("Matrix {0}: {1}x{2} {3}", Name, Rows, Cols, TypeName);
        }

        /// <summary>
        /// The data type of the Matrix 
        /// </summary>
        public MATInvoke.mxClassID Type;
        /// <summary>
        /// The name of the variable
        /// </summary>
        public string Name;
        /// <summary>
        /// The number of rows
        /// </summary>
        public int Rows;
        /// <summary>
        /// The number of columns
        /// </summary>
        public int Cols;
    }

}
