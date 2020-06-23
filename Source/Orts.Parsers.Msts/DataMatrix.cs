// COPYRIGHT 2010, 2011, 2012 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Orts.Parsers.Msts
{
    /// <summary>
    /// Matrix of values, for instance speed versus field
    /// </summary>
    public class DataMatrix
    {
        float[] X;  // must be in increasing order
        public float[] Y;
        float[] Y2;
        int Size;       // number of values populated
        int PrevIndex;  // used to speed up repeated evaluations with similar x values
        public DataMatrix(int n)
        {
            X = new float[n];
            Y = new float[n];
        }
        public DataMatrix(float[] x, float[] y)
        {
            X = x;
            Y = y;
            Size = X.Length;
        }
        public DataMatrix(DataMatrix other)
        {
            X = other.X;
            Y = other.Y;
            Y2 = other.Y2;
            Size = other.Size;
        }
        public DataMatrix(STFReader stf)
        {
            List<float> list = new List<float>();
            stf.MustMatch("(");
            while (!stf.EndOfBlock())
                list.Add(stf.ReadFloat(STFReader.UNITS.Any, null));
            if (list.Count % 2 == 1)
                STFException.TraceWarning(stf, "Ignoring extra odd value in Matrix list.");
            int n = list.Count / 2;
            if (n < 2)
                STFException.TraceWarning(stf, "Matrix must have at least two value pairs.");
            X = new float[n];
            Y = new float[n];
            Size = n;
            for (int i = 0; i < n; i++)
            {
                X[i] = list[2 * i];
                Y[i] = list[2 * i + 1];
//                if (i > 0 && X[i - 1] >= X[i])
//                    STFException.TraceWarning(stf, "Matrix x values must be increasing.");
            }
        }
        public float this[float x]
        {
            get
            {
                float value = 0;
                for (int i = 0; i < this.Size; i++)
                    if (x > X[i])
                    {
                        value = Y[i];
                    }
                return value;
            }
            set
            {
                X[Size] = x;
                Y[Size] = value;
                Size++;
            }
        }

        public float Get(float x)
        {
            //** Getting the value for x    **//
            float value=Y[0];
            for(int i=0; i<Size; i++)
            {
                if (x >= X[i])
                {
                    value = Y[i];
                }
            }
            return value;
        }

        public void Set(float x, float value)
        {
            X[Size] = x;
            Y[Size] = value;
            Size++;
        }
        public float MinX() { return X[0]; }
        public float MaxX() { return X[Size - 1]; }
        public float MaxY()
        {
            float x;
            return MaxY(out x);
        }
        public float MaxY(out float x)
        {
            int maxi = 0;
            for (int i = 1; i < Size; i++)
                if (Y[maxi] < Y[i])
                    maxi = i;
            x = X[maxi];
            return Y[maxi];
        }
        public bool HasNegativeValue()
        {
            for (int i = 1; i < Size; i++)
            {
                if (Y[i] < 0)
                    return true;
            }
            return false;
        }
        public void ScaleX(float factor)
        {
            for (int i = 0; i < Size; i++)
                X[i] *= factor;
        }
        public void ScaleY(float factor)
        {
            for (int i = 0; i < Size; i++)
                Y[i] *= factor;
            if (Y2 != null)
            {
                for (int i = 0; i < Size; i++)
                    Y2[i] *= factor;
            }
        }
        public void ComputeSpline()
        {
            ComputeSpline(null, null);
        }
        public void ComputeSpline(float? yp1, float? yp2)
        {
            Y2 = new float[Size];
            float[] u = new float[Size];
            if (yp1 == null)
            {
                Y2[0] = 0;
                u[0] = 0;
            }
            else
            {
                Y2[0] = -.5f;
                float d = X[1] - X[0];
                u[0] = 3 / d * ((Y[1] - Y[0]) / d - yp1.Value);
            }
            for (int i = 1; i < Size - 1; i++)
            {
                float sig = (X[i] - X[i - 1]) / (X[i + 1] - X[i - 1]);
                float p = sig * Y2[i - 1] + 2;
                Y2[i] = (sig - 1) / p;
                u[i] = (6 * ((Y[i + 1] - Y[i]) / (X[i + 1] - X[i]) -
                    (Y[i] - Y[i - 1]) / (X[i] - X[i - 1])) / (X[i + 1] - X[i - 1]) -
                    sig * u[i - 1]) / p;
            }
            if (yp2 == null)
            {
                Y2[Size - 1] = 0;
            }
            else
            {
                float d = X[Size - 1] - X[Size - 2];
                Y2[Size - 1] = (3 / d * (yp2.Value - (Y[Size - 1] - Y[Size - 2]) / d) - .5f * u[Size - 2]) / (.5f * Y2[Size - 2] + 1);
            }
            for (int i = Size - 2; i >= 0; i--)
                Y2[i] = Y2[i] * Y2[i + 1] + u[i];
        }

        // restore game state
        public DataMatrix(BinaryReader inf)
        {
            Size = inf.ReadInt32();
            X = new float[Size];
            Y = new float[Size];
            for (int i = 0; i < Size; i++)
            {
                X[i] = inf.ReadSingle();
                Y[i] = inf.ReadSingle();
            }
            if (inf.ReadBoolean())
            {
                Y2 = new float[Size];
                for (int i = 0; i < Size; i++)
                    Y2[i] = inf.ReadSingle();
            }
        }

        // save game state
        public void Save(BinaryWriter outf)
        {
            outf.Write(Size);
            for (int i = 0; i < Size; i++)
            {
                outf.Write(X[i]);
                outf.Write(Y[i]);
            }
            outf.Write(Y2 != null);
            if (Y2 != null)
                for (int i = 0; i < Size; i++)
                    outf.Write(Y2[i]);
        }

        public void test(string label, int n)
        {
            float dx = (MaxX() - MinX()) / (n - 1);
            for (int i = 0; i < n; i++)
            {
                float x = MinX() + i * dx;
                float y = this[x];
                Console.WriteLine("{0} {1} {2}", label, x, y);
            }
        }

        public int GetSize()
        {
            if (X.Length == Y.Length)
                return Size;
            else
                return -1;
        }
    }
}

    

