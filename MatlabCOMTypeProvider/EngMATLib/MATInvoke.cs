using System;
using System.Runtime.InteropServices;


namespace EngMATLib
{
    /// <summary>
    /// A P/Invoke interface class for the three groups of MATLAB APIs
    /// - libmx
    /// - libmat
    /// - libeng
    /// </summary>
    public class MATInvoke
    {
        #region Engine Functions
        [DllImport("libeng.dll")]
        internal static extern IntPtr engOpen(string startcmd);

        [DllImport("libeng.dll")]
        internal static extern IntPtr engClose(IntPtr e);

        [DllImport("libeng.dll")]
        internal static extern int engEvalString(IntPtr e, string cmd);

        [DllImport("libeng.dll")]
        internal static extern void engSetVisible(IntPtr e, bool q);

        [DllImport("libeng.dll")]
        internal static extern bool engIsVisible(IntPtr e);

        [DllImport("libeng.dll", CharSet = CharSet.Ansi)]
        internal static extern int engOutputBuffer(IntPtr e, IntPtr buffer, int n);

        // OBSOLETED from 6.5
        [DllImport("libeng.dll")]
        internal static extern int engPutArray(IntPtr e, IntPtr array);

        // REQUIRES 6.5+
        [DllImport("libeng.dll")]
        internal static extern int engPutVariable(IntPtr e, string name, IntPtr array);

        // REQUIRES 6.5+
        [DllImport("libeng.dll")]
        internal static extern IntPtr engGetVariable(IntPtr e, string name);

        // OBSOLETED from 6.5
        [DllImport("libeng.dll")]
        internal static extern IntPtr engGetArray(IntPtr e, string name);
        #endregion

        #region MX Functions
        // MATRIX FUNCTIONS (libmx.dll)
        /// <summary>
        /// The Type of Matrix
        /// </summary>
        public enum mxComplexity
        {
            /// <summary>
            /// real matrix
            /// </summary>
            mxREAL,
            /// <summary>
            /// complex matrix
            /// </summary>
            mxCOMPLEX
        }

        /// <summary>
        /// Type of Matrix elements
        /// </summary>
        public enum mxClassID
        {
            UNKNOWN,
            CELL,
            STRUCT,
            OBJECT,
            CHAR,
            SPARSE,
            DOUBLE,
            SINGLE,
            INT8,
            UINT8,
            INT16,
            UINT16,
            INT32,
            UINT32,
            INT64,		/* place holder - future enhancements */
            UINT64,		/* place holder - future enhancements */
            FUNCTION,
            OPAQUE
        }

        public static mxClassID Type2ClassID(Type t)
        {
            if (t == typeof(ulong))
                return mxClassID.UINT64;
            else if (t == typeof(long))
                return mxClassID.INT64;
            else if (t == typeof(uint))
                return mxClassID.UINT32;
            else if (t == typeof(int))
                return mxClassID.INT32;
            else if (t == typeof(short))
                return mxClassID.INT16;
            else if (t == typeof(ushort))
                return mxClassID.UINT16;
            else if (t == typeof(byte))
                return mxClassID.UINT8;
            else if (t == typeof(sbyte))
                return mxClassID.INT8;
            else if (t == typeof(float))
                return mxClassID.SINGLE;
            else if (t == typeof(double))
                return mxClassID.DOUBLE;
            else
                return mxClassID.UNKNOWN;

        }

        // Creates a Matrix with the specified dimensions
        [DllImport("libmx.dll")]
        internal static extern IntPtr mxCreateDoubleMatrix(int n, int m, mxComplexity c);

        // Creates a Matrix with the specified dimensions
        [DllImport("libmx.dll")]
        internal static extern IntPtr mxCreateNumericMatrix(int n, int m, mxClassID classid, mxComplexity c);

        // Creates a Multidimensional Matrix with the specific type
        [DllImport("libmx.dll")]
        internal static extern IntPtr mxCreateNumericArray(int ndim, int[] dims, mxClassID classid, mxComplexity flag);

        // Destroy a Matrix
        [DllImport("libmx.dll")]
        internal static extern int mxDestroyArray(IntPtr pa);

        // Sets the name of a Matrix
        [DllImport("libmx.dll")]
        internal static extern int mxSetName(IntPtr pa, string name);

        // Gets the name of a Matrix
        [DllImport("libmx.dll")]
        internal static extern string mxGetName(IntPtr pa);

        // Get the raw pointer to the matrix data as a double *
        [DllImport("libmx.dll")]
        internal static extern IntPtr mxGetPr(IntPtr pa);

        // Gets the raw pointer to the matrix data as a void *
        [DllImport("libmx.dll")]
        public static extern IntPtr mxGetData(IntPtr pa);

        // Gets the number of columns
        [DllImport("libmx.dll")]
        internal static extern int mxGetN(IntPtr pa);

        // Gets the number of rows
        [DllImport("libmx.dll")]
        internal static extern int mxGetM(IntPtr pa);

        // Gets The type of the matrix data
        [DllImport("libmx.dll")]
        internal static extern mxClassID mxGetClassID(IntPtr pa);

        [DllImport("libmx.dll")]
        internal static extern bool mxIsEmpty(IntPtr a);
        #endregion

        #region MAT Functions
        [DllImport("libmat.dll")]
        internal static extern int matClose(IntPtr mat);

        [DllImport("libmat.dll")]
        internal static extern IntPtr matOpen(string filename, string mode);

        [DllImport("libmat.dll")]
        internal static extern int matPutArray(IntPtr mat, IntPtr mtx);

        // R13 only
        //[DllImport("libmat.dll")]
        //internal static extern int matPutVariable(IntPtr mat, IntPtr mtx);

        [DllImport("libmat.dll")]
        internal static extern IntPtr matGetArray(IntPtr mat, string name);

        [DllImport("libmat.dll")]
        internal static extern IntPtr matGetArrayHeader(IntPtr mat, string name);

        [DllImport("libmat.dll")]
        internal static extern IntPtr matGetNextArrayHeader(IntPtr mat);

        [DllImport("libmat.dll")]
        internal static extern IntPtr matGetNextArray(IntPtr mat, IntPtr mtx);

        [DllImport("libmat.dll")]
        internal static extern int matDeleteArray(IntPtr mat, string name);

        #endregion

    }
}
