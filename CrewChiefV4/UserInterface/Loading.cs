using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrewChiefV4.UserInterface
{
    public partial class Loading : Form
    {
        public static string splashImageFolderPath = DataFiles.LocalApplicationDataFolder;
        public static string tempSplashImagePath = Path.Combine(splashImageFolderPath, "splash_image_tmp.png");
        public static string splashImagePath = Path.Combine(splashImageFolderPath, "splash_image.png");
        public Loading()
        {
            ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.Icon = ((Icon)(resources.GetObject("$this.Icon")));

            InitializeComponent();
        }
    }
}
