using System;
using System.IO;
using Jazztures.Core.Melody;
using UnityEngine;

namespace Jazztures.App
{
    /// <summary>
    /// Writes one palm-swing sandbox run to a CSV under <c>Application.persistentDataPath</c> (ADR-0078): the header,
    /// then a row per scored event. Events only — never in the per-frame path. A failed write is logged once and the
    /// run goes on: the learner never learns the log dropped (§4.4 in spirit). Classifications and timestamps only;
    /// no raw joint data (§4.1).
    /// </summary>
    internal sealed class PalmSwingCsvLogger : IDisposable
    {
        private StreamWriter _writer;
        private bool _failed;

        public string Path { get; private set; }

        public void Open(string label)
        {
            Close();
            _failed = false;
            try
            {
                string name = $"PalmSwing_{DateTime.Now:yyyyMMdd_HHmmss}_{label}.csv";
                Path = System.IO.Path.Combine(Application.persistentDataPath, name);
                _writer = new StreamWriter(Path, append: false) { AutoFlush = true };
                _writer.WriteLine(DrillCsv.Header);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        public void Write(string row)
        {
            if (_writer == null || _failed)
            {
                return;
            }

            try
            {
                _writer.WriteLine(row);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        public void Close()
        {
            try
            {
                _writer?.Dispose();
            }
            catch (Exception e)
            {
                Fail(e);
            }

            _writer = null;
        }

        public void Dispose() => Close();

        private void Fail(Exception e)
        {
            if (!_failed)
            {
                Debug.LogWarning($"PalmSwing CSV: {e.Message}; this run will not be logged.");
            }

            _failed = true;
            _writer = null;
        }
    }
}
