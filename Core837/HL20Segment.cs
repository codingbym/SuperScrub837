using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SuperScrub837.Core837
{
    public class HL20Segment
    {
        //creating this as an internal index/id reference for things like the custom synchronizer group
        private int index;
        public int Index { get => index; set => index = value; }

        //variable that stores of the full text of the hierarchical segment
        private string segmentText;
        public string SegmentText { get => segmentText; set => segmentText = value; }

        //of course this is the claim number!
        private string claimNumber;
        public string ClaimNumber { get => claimNumber; set => claimNumber = value; }

        //2000A section
        private string billingProvider;
        public string BillingProvider { get => billingProvider; set => billingProvider = value; }

        //2000B section
        private string subscriberSegment;
        public string SubscriberSegment { get => subscriberSegment; set => subscriberSegment = value; }

        //2300 Claim Information section - will need if we want to do 2310 validation checks
        private string claim2300;
        public string Claim2300 { get => claim2300; set => claim2300 = value; }

        //2320 onwards - other subscribers
        private List<string> otherSubscribers = new List<string>();
        public List<string> OtherSubscribers { get => otherSubscribers; set => otherSubscribers = value; }

        //this is what is visually seen in the application
        private string errorText;
        public string ErrorText { get => errorText; set => errorText = value; }

        //All line items for the claim are stored in this property
        private List<string> lineItems = new List<string>();
        public List<string> LineItems { get => lineItems; set => lineItems = value; }

        //this is used inside the filter logic of the WPF application
        private List<string> errors = new List<string>();
        public List<string> Errors { get => errors; set => errors = value; }

        //this is the line marker array for rendering the lines a claim segment belongs to in the file
        private List<int> lineMarkers = new List<int>();
        public List<int> LineMarkers { get => lineMarkers; set => lineMarkers = value; }

        //creating a dictionary with line number and equivalent segment line pairs
        private Dictionary<int, string> line837Matrix = new Dictionary<int, string>();
        public Dictionary<int, string> Line837Matrix { get => line837Matrix; set => line837Matrix = value; }

        //creating a NEW variable to help the converters determine whether a textbox is visible or its textblock counterpart
        private bool txtBoxFlag = false;
        public bool TxtBoxFlag { get => txtBoxFlag; set => txtBoxFlag = value; }

        public Dictionary<int, string> create837Matrix(string segment, List<int> lineNums)
        {
            Dictionary<int, string> new837Matrix = new Dictionary<int, string>();

            string[] segmentArr = Regex.Split(segment, @"\r\n");

            for(int i=0; i<segmentArr.Length; i++)
            {
                new837Matrix.Add(lineNums[i], segmentArr[i]);
            }

            return new837Matrix;
        }

        //public HL20Segment()
        //{
        //    Line837Matrix = create837Matrix(SegmentText, LineMarkers);
        //}

    }

    public class HL20Segments : ObservableCollection<HL20Segment>
    {
        //now observable
    }

}
