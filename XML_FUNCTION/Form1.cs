using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Xml.Linq;
namespace XML_FUNCTION
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        static void UPDATEXML()
        {
            XElement xElement = XElement.Load("XMLFile1.xml");
            List<Book> books = (from q in xElement.Elements("validator")
                                select new Book
                                {
                                    Title = q.Element("name").Value,
                                    
                                   
                                }).ToList();
            foreach (Book b in books)
            {
                Console.WriteLine(b.Title + "-" + b.max);
            }

        }

        private void button1_Click(object sender, EventArgs e)
        {
            UPDATEXML();
        }
    }
    class Book
    {
        public string Title { get; set; }
      
        public int max { get; set; }
    }
}
