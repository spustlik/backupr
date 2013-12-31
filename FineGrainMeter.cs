using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Data;

namespace Backupr
{
    /// <summary>
    /// fine-grain meter for detailed metering of your code
    /// at the end, it will write one log record with human-readable table
    /// </summary>
    public sealed class PerformanceMeter : IDisposable
    {
        #region static part
        [ThreadStatic]
        private static PerformanceMeter _currentThreadMeter;
        public static PerformanceMeter Perform(string name)
        {
            var result = new PerformanceMeter(_currentThreadMeter, name);
            _currentThreadMeter = result;
            return result;
        }
        public static bool Stop(PerformanceMeter meter)
        {
            if (_currentThreadMeter != meter)
                return false;
            _currentThreadMeter = meter._parent;
            return true;
        }

        public static void PerformancePoint(string name)
        {
            if (_currentThreadMeter == null)
            {
                throw new InvalidOperationException("Cannot create PerformPoint if there is no running meter. Use PerformanceMeter.Perform to create one.");
            }
            _currentThreadMeter.CreatePoint(name);
        }
        #endregion

        private Stopwatch _stopwatch;
        private string _name;
        private PerformanceMeter _parent;
        private List<PerformanceMeterData> _children = new List<PerformanceMeterData>();

        public TimeSpan Elapsed { get { return _stopwatch.Elapsed; } }

        public PerformanceMeter(PerformanceMeter parent, string name)
        {
            _parent = parent;
            _name = name;
            _stopwatch = Stopwatch.StartNew();
        }

        public void CreatePoint(string name)
        {
            _children.Add(new PerformanceMeterData("*" + name, _stopwatch.Elapsed, new PerformanceMeterData[0], isPoint: true));
        }

        public void Dispose()
        {
            if (_stopwatch.IsRunning)
            {
                Stop();
            }
        }

        public void Stop()
        {
            _stopwatch.Stop();

            PerformanceMeter.Stop(this);
            var data = CurrentPerformanceData;
            if (_parent != null)
            {
                _parent._children.Add(data);
            }
            WriteToLog(data, isRoot: _parent == null);
        }

        public PerformanceMeterData CurrentPerformanceData
        {
            get
            {
                var data = new PerformanceMeterData(_name, _stopwatch.Elapsed, _children.ToArray(), isPoint: false);
                return data;
            }
        }

        private void WriteToLog(PerformanceMeterData data, bool isRoot)
        {
            {
                Debug.WriteLine(data.ToDebugString());
            }
        }
    }

    public interface IPerformanceReporter
    {
        void ReportPerformanceData(PerformanceMeterData data, bool isRoot);
    }

    public sealed class PerformanceMeterData
    {
        public string Name { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public PerformanceMeterData[] Children { get; private set; }
        public bool IsPoint { get; private set; }

        public PerformanceMeterData(string name, TimeSpan elapsed, PerformanceMeterData[] children, bool isPoint)
        {
            Name = name;
            Elapsed = elapsed;
            Children = children;
            IsPoint = isPoint;
        }

        public DataSet AsDataSet
        {
            get
            {
                var ds = new DataSet();
                var data = GetDataTable();
                ds.Tables.Add(data);
                ds.Tables.Add(GetStatTable(data));
                return ds;
            }
        }

        public DataTable GetStatTable(DataTable data)
        {
            var stats = new DataTable("Statistics");
            stats.Columns.Add("Name");
            stats.Columns.Add("Count", typeof(long));
            stats.Columns.Add("Total", typeof(double));
            stats.Columns.Add("Own", typeof(double));
            stats.Columns.Add("AvgOwn", typeof(double));
            stats.Columns.Add("VarianceOwn", typeof(double));
            stats.Columns.Add("MinOwn", typeof(double));
            stats.Columns.Add("MaxOwn", typeof(double));

            foreach (var group in data.Rows.Cast<DataRow>().GroupBy(row => row.Field<string>("Name")))
            {
                var count = group.Count();
                var ownData = group.Select(row => row.Field<double>("Own")).ToArray();
                var own = ownData.Sum();
                var sumsquare = ownData.Sum(d => d * d);
                stats.Rows.Add(
                    group.Key,
                    count,
                    group.Sum(row => row.Field<double>("Total")),
                    own,
                    own / count,
                    Math.Sqrt((sumsquare - own * own / count) / count),
                    ownData.Min(),
                    ownData.Max()
                    );
            }
            return stats;
        }

        public DataTable GetDataTable()
        {
            var table = new DataTable("Data");
            table.Columns.Add("Text");
            table.Columns.Add("Name");
            table.Columns.Add("Level", typeof(int));
            table.Columns.Add("Total", typeof(double));
            table.Columns.Add("Own", typeof(double));
            table.Columns.Add("IsPoint", typeof(bool));
            GetDataRec(table, 0);
            return table;
        }

        private void GetDataRec(DataTable table, int level)
        {
            var elapsedWithoutchildren = (Elapsed - Children.Where(c => !c.IsPoint).Aggregate(TimeSpan.Zero, (a, c) => a + c.Elapsed));
            table.Rows.Add(new object[]{
                "".PadLeft(level)+Name,
                Name,
                level,
                Elapsed.TotalMilliseconds,
                elapsedWithoutchildren.TotalMilliseconds,
                IsPoint
                });
            foreach (var item in Children)
            {
                item.GetDataRec(table, level + 1);
            }
        }

        public void Write(System.IO.TextWriter s, Action<TextWriter, DataTable> dataTableWriter = null)
        {
            if (dataTableWriter == null)
                dataTableWriter = WriteDataTableToTabbedText;
            var data = GetDataTable();
            var stat = GetStatTable(data);
            //WriteDataTableToTabbedText(s, data);
            dataTableWriter(s, stat);
        }

        public override string ToString()
        {
            using (var s = new System.IO.StringWriter())
            {
                Write(s);
                s.Flush();
                return s.GetStringBuilder().ToString();
            }
        }
        public static void WriteDataTableToTabbedText(System.IO.TextWriter s, DataTable data)
        {
            foreach (DataRow row in data.Rows)
            {
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    s.Write(row[i]);
                    s.Write("\t");
                }
                s.WriteLine();
            }
        }

        public static void WriteDataTableToSpacedText(System.IO.TextWriter s, DataTable data)
        {
            var columnWidths = data.Columns.OfType<DataColumn>().Select(c => c.Caption.Length).ToArray();
            foreach (DataRow row in data.Rows)
            {
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    var value = row[i] + "";
                    columnWidths[i] = Math.Max(columnWidths[i], value.Length);
                }
            }

            for (int i = 0; i < data.Columns.Count; i++)
            {
                var value = data.Columns[i].Caption;
                WriteColumnValue(s, value, columnWidths, i);
            }
            s.WriteLine();
            foreach (DataRow row in data.Rows)
            {
                for (int i = 0; i < data.Columns.Count; i++)
                {
                    var value = row[i] + "";
                    WriteColumnValue(s, value, columnWidths, i);
                }
                s.WriteLine();
            }
        }

        private static void WriteColumnValue(TextWriter s, string value, int[] columnWidths, int columnIndex)
        {
            s.Write(value);
            s.Write(new string(' ', columnWidths[columnIndex] - value.Length));
            if (columnIndex != columnWidths.Length)
            {
                s.Write(" | ");
            }
        }

        public string ToDebugString()
        {
            using (var s = new System.IO.StringWriter())
            {
                Write(s, WriteDataTableToSpacedText);
                s.Flush();
                return s.GetStringBuilder().ToString();
            }

        }


    }

}
