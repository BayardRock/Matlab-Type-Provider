// Matlab Interface Library
// by Emanuele Ruffaldi 2002
// http://www.sssup.it/~pit/
// mailto:pit@sssup.it
//
// Description: class that encapsulates a .MAT file Access
using System;
using System.Collections.Specialized;

namespace EngMATLib
{
    /// <summary>
    /// File Access modes for MAT files
    /// </summary>
    public enum FileAccess
    {
        /// <summary>
        /// Read Only
        /// </summary>
        Read,
        /// <summary>
        /// Read And Write, the original file version is kept
        /// </summary>
        Update,
        /// <summary>
        /// Write only, if the file exits, it's cleared
        /// </summary>
        Write,
        /// <summary>
        /// Write a MATLAB v4 file
        /// </summary>
        Write4
    };


    /// <summary>
    /// A Library for .MAT file Access
    /// </summary>
    public class MATFile : IDisposable
    {

        /// <summary>
        /// No file
        /// </summary>
        public MATFile()
        {
            mat = IntPtr.Zero;
            filename = null;
            mode = FileAccess.Read;
        }

        /// <summary>
        /// Open the specific file
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="fa"></param>
        public MATFile(string filename, FileAccess fa)
        {
            mode = FileAccess.Read;
            mat = IntPtr.Zero;
            Open(filename, fa);
        }

        /// <summary>
        /// ReOpen a file (rewind), except for Write modes
        /// </summary>
        /// <returns></returns>
        public bool ReOpen()
        {
            if (mode == FileAccess.Write || mode == FileAccess.Write4)
                return false;
            Close();
            return Open(filename, mode);
        }

        /// <summary>
        /// Opens the specific file
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="fa"></param>
        /// <returns></returns>
        public bool Open(string filename, FileAccess fa)
        {
            if (IsOpened)
                return false;
            string smode = null;
            switch (fa)
            {
                case FileAccess.Read: smode = "r"; break;
                case FileAccess.Write: smode = "w"; break;
                case FileAccess.Write4: smode = "w4"; break;
                case FileAccess.Update: smode = "u"; break;
            }
            this.filename = filename;
            this.mode = fa;
            mat = MATInvoke.matOpen(filename, smode);
            return IsOpened;
        }

        /// <summary>
        /// The property of all the Variables in a MAT file
        /// Can be called only as first operation
        /// </summary>
        public NamedMatrixCollection Variables
        {
            get
            {
                NamedMatrixCollection oc = new NamedMatrixCollection();
                if (mode == FileAccess.Write || mode == FileAccess.Write4)
                    return oc;

                if (!IsOpened)
                    ReOpen();

                if (IsOpened)
                {
                    IntPtr p;
                    while ((p = MATInvoke.matGetNextArrayHeader(mat)) != IntPtr.Zero)
                    {
                        MatrixDescription md = new MatrixDescription(p);
                        oc[md.Name] = md;
                        MATInvoke.mxDestroyArray(p);
                    }
                    ReOpen();
                }
                return oc;
            }
        }

        /// <summary>
        /// Destroy a matrix variable by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool DestroyMatrix(string name)
        {
            if (!IsOpened || mode == FileAccess.Read) return false;
            return MATInvoke.matDeleteArray(mat, name) == 0;
        }

        /// <summary>
        /// Gets a matrix variable info
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public MatrixDescription GetMatrixInfo(string name)
        {
            IntPtr pa = MATInvoke.matGetArrayHeader(mat, name);
            MatrixDescription md = new MatrixDescription(pa);
            MATInvoke.mxDestroyArray(pa);
            return md;
        }

        /// <summary>
        /// Get a Matrix variable
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool GetMatrix(string name, ref double[,] data)
        {
            if (!IsOpened || (mode == FileAccess.Write || mode == FileAccess.Write4)) return false;
            IntPtr pa;
            pa = MATInvoke.matGetArray(mat, name);
            if (pa == IntPtr.Zero)
                return false;
            bool b = EngMATAccess.MxArrayToMatrix(pa, ref data);
            MATInvoke.mxDestroyArray(pa);
            return b;
        }

        /// <summary>
        /// Stores a matrix variable
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool PutMatrix(string name, double[,] data)
        {
            if (!IsOpened || (mode == FileAccess.Read)) return false;
            IntPtr pa = EngMATAccess.MatrixToMxArray(data, false);
            if (pa == IntPtr.Zero)
                return false;
            MATInvoke.mxSetName(pa, name);
            bool b = MATInvoke.matPutArray(mat, pa) == 0;
            MATInvoke.mxDestroyArray(pa);
            return b;
        }

        /// <summary>
        /// Tells if the file is opened
        /// </summary>
        public bool IsOpened
        {
            get { return mat != IntPtr.Zero; }
        }

        /// <summary>
        /// Closes it
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// The Filename of the MATFile object
        /// </summary>
        public string Filename
        {
            get { return filename; }
        }

        protected virtual void Dispose(bool disp)
        {
            if (IsOpened)
            {
                MATInvoke.matClose(mat);
                mat = IntPtr.Zero;
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

        ~MATFile()
        {
            Dispose(false);
        }


        IntPtr mat;
        string filename;
        FileAccess mode;
    }

    /// <summary>
    /// A Collection of String --&gt; MatrixDescription objects
    /// </summary>
    public class NamedMatrixCollection : NameObjectCollectionBase
    {
        public MatrixDescription this[string s]
        {
            get { return (MatrixDescription)BaseGet(s); }
            set { BaseSet(s, value); }
        }

        public void Clear()
        {
            BaseClear();
        }
    };

}
