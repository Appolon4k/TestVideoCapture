using System;
using System.Drawing;
using System.Windows.Forms;

namespace TestVideoCapture
{
    public partial class FormScreenshot : Form
    {
        public string FileName { get; set; }

        public MainForm.ImageType Extension { get; set; }
        public string Description { get; set; }

        public FormScreenshot(Bitmap image)
        {

           
            InitializeComponent();
            FileName = DateTime.Now.ToString("HH-mm-ss");

            this.StartPosition = FormStartPosition.CenterParent;
            this.Width = 600;
            this.Height = 600;

            comboBox1.SelectedIndex = 0;
            pictureBox.Image = image;

            
            Extension = MainForm.ImageType.Jpeg;


        }

        
        
        private void FormScreenshot_Load(object sender, EventArgs e)
        {
            textBoxFileName.Text = FileName;

        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            //new description 
            Description = textBoxDescription.Text;
            FileName = textBoxFileName.Text;
            
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboBox1.SelectedIndex)
            {
                case 0://jpeg
                    Extension = MainForm.ImageType.Jpeg;
                    break;
                case 1://png
                    Extension = MainForm.ImageType.Png;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


    }
}
