using PhilipsPatternRom.Converter;
using PhilipsPatternRom.Converter.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PhilipsPatternRom.StripeGenerator
{
    public partial class StripeGeneratorForm : Form
    {
        private PatternRenderer _renderer;
        private bool _withClock;

        public StripeGeneratorForm()
        {
            InitializeComponent();
            _withClock = false;
        }

        private void btnLoadPattern_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.SelectedPath = Properties.Settings.Default.LastOpenDir;

                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK)
                {
                    Properties.Settings.Default.LastOpenDir = fbd.SelectedPath;
                    Properties.Settings.Default.LastGeneratorType = ddlGeneratorType.SelectedIndex;
                    Properties.Settings.Default.Save();

                    LoadPattern();
                }
            }
        }

        private void LoadPattern()
        {
            _renderer = new PatternRenderer();
            _renderer.LoadPattern((Converter.Models.GeneratorType)ddlGeneratorType.SelectedIndex, Properties.Settings.Default.LastOpenDir);

            Render();
        }

        private void Render()
        {
            if (_renderer == null)
                return;

            var bitmap = _renderer.GeneratePatternComponents();

            imgPattern.Image = bitmap.Luma;
        }

        private void ParseAndRender()
        {
            _renderer.SetMarkers((int)nudHorizontalStart.Value, (int)nudHorizontalEnd.Value, (int)nudVerticalStart.Value, (int)nudVerticalEnd.Value);

            Properties.Settings.Default.HorizontalStart = (int)nudHorizontalStart.Value;
            Properties.Settings.Default.HorizontalEnd = (int)nudHorizontalEnd.Value;

            Properties.Settings.Default.VerticalStart = (int)nudVerticalStart.Value;
            Properties.Settings.Default.VerticalEnd = (int)nudVerticalEnd.Value;

            Properties.Settings.Default.Save();

            Render();
        }

        private void StripeGeneratorForm_Load(object sender, EventArgs e)
        {
            ddlGeneratorType.SelectedIndex = Properties.Settings.Default.LastGeneratorType;

            nudHorizontalStart.Value = Properties.Settings.Default.HorizontalStart;
            nudHorizontalEnd.Value = Properties.Settings.Default.HorizontalEnd;

            nudVerticalStart.Value = Properties.Settings.Default.VerticalStart;
            nudVerticalEnd.Value = Properties.Settings.Default.VerticalEnd;

            if (!string.IsNullOrEmpty(Properties.Settings.Default.LastOpenDir))
            {
                LoadPattern();
            }
        }

        private void btnRedraw_Click(object sender, EventArgs e)
        {
            ParseAndRender();
        }

        private void btnWritePng_Click(object sender, EventArgs e)
        {
            imgPattern.Image.Save(Path.Combine(Properties.Settings.Default.LastOpenDir, "Rendered.png"), ImageFormat.Png);
        }

        private void btnSaveAs_Click(object sender, EventArgs e)
        {
            try
            {
                var set = _renderer.GetStripeSet();

                if (set == null)
                    throw new Exception("No stripe set available to save");

                var saveFileDialog = new SaveFileDialog();

                saveFileDialog.Filter = "XML files (*.xml)|*.xml";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.RestoreDirectory = true;
                saveFileDialog.InitialDirectory = Properties.Settings.Default.LastOpenDir;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(typeof(StripeSet));
                    FileStream file = File.Create(saveFileDialog.FileName);
                    writer.Serialize(file, set);
                    file.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();

            openFileDialog.InitialDirectory = "c:\\";
            openFileDialog.Filter = "XML files (*.xml)|*.xml";
            openFileDialog.FilterIndex = 1;
            openFileDialog.InitialDirectory = Properties.Settings.Default.LastOpenDir;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(StripeSet));

                using (Stream reader = new FileStream(openFileDialog.FileName, FileMode.Open))
                {
                    var set = (StripeSet)serializer.Deserialize(reader);

                    nudHorizontalStart.Value = set.HorizontalStart;
                    nudHorizontalEnd.Value = set.HorizontalEnd;
                    nudVerticalStart.Value = set.VerticalStart;
                    nudVerticalEnd.Value = set.VerticalEnd;

                    ParseAndRender();
                }
            }
        }

        private void chkClockCutout_CheckedChanged(object sender, EventArgs e)
        {
            _withClock = chkClockCutout.Checked;
            Render();
        }

        private void ddlGeneratorType_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.LastGeneratorType = ddlGeneratorType.SelectedIndex;
            Properties.Settings.Default.Save();
            LoadPattern();
        }

        private void nudManualOffset_ValueChanged(object sender, EventArgs e)
        {
            _renderer.Offset = (int)nudManualOffset.Value;
            Render();
        }
    }
}
