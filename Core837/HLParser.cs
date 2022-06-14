using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SuperScrub837.Core837
{
    public static class HLParser
    {
        

        /// <summary>
        /// Here is the logic that actually loads the claim segments as matched through regular expressions from the given file
        /// It is either called automatically when loading a claim file or can be called manually when one wants to see an
        /// alternative break or when the initial automatic break fails due to the file's formatting.
        /// </summary>
        /// <param name="claimFilePath"></param>
        /// <param name="breakingPreference"></param>
        /// <returns></returns>
        public static Tuple<HeadFoot837, ObservableCollection<HL20Segment>, string> loadSegments(string claimFilePath, string breakingPreference)
        {
            ObservableCollection<HL20Segment> subSegments = new ObservableCollection<HL20Segment>();

            HeadFoot837 envelope = new HeadFoot837();

            string delimiter = "\\";

            StreamReader reader = File.OpenText(claimFilePath);

            string rawANSI = reader.ReadToEnd();

            delimiter = Regex.Match(rawANSI, @"(?<=ISA)([\s\S])").ToString();

            //this is a robust check for multiple footer segments
            string split837Regex = "";
            string footer = "";
            var split837FooterCheck = Regex.Split(rawANSI, @"\r\n(?=SE\" + delimiter + @"\d*\" + delimiter + @"\d*[~\r\n]+)").ToList();

            if(split837FooterCheck.Count == 2)
            {
                split837Regex = breakingPreference + @"|(?=SE\" + delimiter + @"\d*\" + delimiter + @"\d*[~\r\n]+[\s\S]*$)|[\r\n]+(?=$)";
                footer = Regex.Match(rawANSI, @"(SE\" + delimiter + @"\d*\" + delimiter + @"\d*[~\r\n]+[\s\S]*)").ToString();
            }
            //there are multiple footers, so best to split on the last one
            else
            {
                footer = split837FooterCheck.Last();
                split837FooterCheck.RemoveAt(split837FooterCheck.Count - 1);
                //going to substitute the 'raw ansi' with a joined version of all but the last element in the footer check
                //that is, IF the footer check contains more than one element
                if(split837FooterCheck.Count > 0)
                    rawANSI = string.Join("", split837FooterCheck);
                //finally, we are only going to split based on the breaking preference line, since the final footer has already been separated
                split837Regex = breakingPreference;
            }

            //throw an error to let the user know if there are still segments below the footer - will probably allow user to place footer at end in near future
            //if the footer only has one line, we're going to give a benefit of a doubt
            if (Regex.Match(footer, @"HL\*").Success && rawANSI == "")
                throw new Exception("There are HL elements in the footer, indicating the footer envelope was not placed properly!");

            int lineBreakCount = Regex.Matches(rawANSI, @"(\r\n)").Count;

            reader.Close();

            //we are going to quickly put line breaks where we can if we don't have the exact return - replacing the footer is a nice visual touch too
            if(lineBreakCount == 0)
            {
                if(Regex.Matches(rawANSI, @"(\r)").Count > 0)
                {
                    rawANSI = Regex.Replace(rawANSI, "(\r)", "\r\n");
                    footer = Regex.Match(rawANSI, @"(SE\" + delimiter + @"\d*\" + delimiter + @"\d*[~\r\n]+[\s\S]*)").ToString();
                }
                else if (Regex.Matches(rawANSI, @"(\n)").Count > 0)
                {
                    rawANSI = Regex.Replace(rawANSI, "(\n)", "\r\n");
                    footer = Regex.Match(rawANSI, @"(SE\" + delimiter + @"\d*\" + delimiter + @"\d*[~\r\n]+[\s\S]*)").ToString();
                }
                //below check for tildes may need to be revised, not sure it will be 100% reliable
                else if (Regex.Matches(rawANSI, @"(~)").Count > 0)
                {
                    rawANSI = Regex.Replace(rawANSI, "(~)", "\r\n");
                    footer = Regex.Match(rawANSI, @"(SE\" + delimiter + @"\d*\" + delimiter + @"\d*[~\r\n]+[\s\S]*)").ToString();
                }
                //we can try to anticipate other characters - I thought of that algorithm, but while I can be wrong I can unfortunately see many problems
                //maybe we can try the algorithm to check for an alternative breakline character in a future update or release
                else
                {
                    throw new Exception("Sorry, but no other breaks other than carriage returns or tildes are supported at this time.");
                }
            }

            string[] rawANSIArray = Regex.Split(rawANSI, split837Regex);

            //the claim marker is going to start 1 more than the number of counted lines in the header segment
            int startClaimMarker = Regex.Matches(rawANSIArray[0], @"(\r\n)").Count + 1;

            int k = 1;

            int loopEndStub = (rawANSIArray.Length / 2) - 1;

            if (loopEndStub == 0)
                loopEndStub = 1; //ensure a file with ONE CLM is analyzed

            for (int i = 0; i<loopEndStub; i++)
            {
                Boolean otherSubscriber = false;
                List<string> subscribersAndLineItems = Regex.Split(rawANSIArray[k + 1], @"(?=(?<=[~\r\n])LX\" + delimiter + @")").ToList();
                //time to check if line items have a CLM or HL in the middle. If they do, we need to deal with this
                List<HL20Segment> extraHLs = new List<HL20Segment>();
                string joinExtras = "";
                var extraCLMCheck = subscribersAndLineItems.GetRange(1, subscribersAndLineItems.Count - 1).Select((extra,idx) => new { extra, idx }).Where(extraCLM => extraCLM.extra.Contains("CLM*")).Select(extraCLM => extraCLM.idx);
                if(extraCLMCheck.Count() != 0)
                {
                    //we're going to load our indices that contain these segments
                    List<int> extraIdx = new List<int>();
                    foreach(var extra in extraCLMCheck)
                    {
                        extraIdx.Add(System.Convert.ToInt32(extra)+1);
                    }
                    //need this as a beginning marker to remove the extra LX-based segments from the original segment later
                    int begLX = (extraIdx[0] + 1);
                    //now we take the extra CLM containing segments off into a separate segment off the line item
                    for (int extraI = 0; extraI < extraIdx.Count; extraI++)
                    {
                        var splitLXfromCLM = Regex.Split(subscribersAndLineItems[extraIdx[extraI]], @"(CLM[\s\S]*$)");
                        subscribersAndLineItems[extraIdx[extraI]] = splitLXfromCLM[0];
                        string extraCLM2300 = splitLXfromCLM[1];
                        List<string> extraCLMLineItems = new List<string>();
                        int nextLX = (extraIdx[extraI] + 1);
                        //need to check if we are on the last extra CLM. If not, we can do the second while loop
                        if ((extraI + 1) == extraIdx.Count)
                        {
                            int begNextLX = nextLX;
                            while (nextLX < subscribersAndLineItems.Count)
                            {
                                extraCLMLineItems.Add(subscribersAndLineItems[nextLX]);
                                nextLX++;
                            }
                        }
                        else
                        {
                            int begNextLX = nextLX;
                            while (nextLX < extraIdx[extraI + 1])
                            {
                                extraCLMLineItems.Add(subscribersAndLineItems[nextLX]);
                                nextLX++;
                            }
                            //finally, we need to split the next CLM segment off from the final line item
                            var splitNextLXfromCLM = Regex.Split(subscribersAndLineItems[nextLX], @"(CLM[\s\S]*$)");
                            extraCLMLineItems.Add(splitNextLXfromCLM[0]);
                        }
                        
                        string extraLX = String.Join("",extraCLMLineItems);
                        string fullExtraCLM = extraCLM2300 + extraLX;
                        //now we add the extra HL segment for later!
                        extraHLs.Add(new HL20Segment
                        {
                            SegmentText = fullExtraCLM,
                            ClaimNumber = Regex.Match(fullExtraCLM, @"(?<=CLM\" + delimiter + @")[\d-]*(?=.*[~\r\n]+)").ToString(),
                            BillingProvider = Regex.Match(rawANSIArray[k] + rawANSIArray[k + 1], @"HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"20\" + delimiter + @".[~\r\n]+[PRV|NM1].*(?:[\s\S])*?(?=HL)").ToString(),
                            SubscriberSegment = "",
                            Claim2300 = extraCLM2300,
                            LineItems = extraCLMLineItems
                            //note, we can't add line markers yet. We will have to add those AFTER the parent segment is added to the actual 837 collection
                        });
                    }
                    //now that we've added the extra LX-segments to the extra CLM segments, we need to remove them from the original set
                    subscribersAndLineItems.RemoveRange(begLX,(subscribersAndLineItems.Count)-begLX);
                    var extraSegments = extraHLs.Select(a => a.SegmentText).ToList();
                    joinExtras = String.Join("",extraSegments);
                }
                List<string> subscribers = Regex.Split(subscribersAndLineItems[0], @"((?<=[~\r\n])SBR\" + delimiter + @".*[~\r\n]+)").ToList();
                //fix for when there is only one primary subscriber - will only remove if there are other subscribers
                string upcomingSubscriberSegment;
                string upcomingOther2300;
                //also need to be sure that the first segment is not a subscriber segment before removing
                if (subscribers.Count > 1 && !subscribers[0].StartsWith("SBR"))
                {
                    subscribers.RemoveAt(0);
                    upcomingSubscriberSegment = subscribers.ElementAt(0) + subscribers.ElementAt(1);
                    upcomingOther2300 = Regex.Match(subscribers.ElementAt(1), @"(CLM[\s\S]*$)").ToString();
                }
                //if there aren't any other subscribers, we'd better prepare the subscriber and other 2300 segments accordingly
                else
                {
                    upcomingSubscriberSegment = subscribers.ElementAt(0);
                    upcomingOther2300 = Regex.Match(subscribers.ElementAt(0), @"(CLM[\s\S]*$)").ToString();
                }
                if (subscribers.Count >= 4)
                {
                    otherSubscriber = true;
                }
                string fullSegment = rawANSIArray[k] + rawANSIArray[k + 1];
                //if there are extra CLM containing segments, remove from the current segment. We'll add these later
                if (extraHLs.Count != 0)
                {
                    int extraPos = fullSegment.IndexOf(joinExtras);
                    fullSegment = fullSegment.Remove(extraPos);
                }
                //need to take final line break off the last segment to match line numbers, small bug
                fullSegment = fullSegment.TrimEnd('\r', '\n');
                
                subSegments.Add(new HL20Segment { SegmentText = fullSegment,
                    ClaimNumber = Regex.Match(rawANSIArray[k + 1], @"(?<=CLM\" + delimiter + @")[\d-]*(?=.*[~\r\n]+)").ToString(),
                    BillingProvider = Regex.Match(rawANSIArray[k] + rawANSIArray[k + 1], @"HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"20\" + delimiter + @".[~\r\n]+[PRV|NM1].*(?:[\s\S])*?(?=HL)").ToString(),
                    SubscriberSegment = upcomingSubscriberSegment,
                    Claim2300 = upcomingOther2300,
                    LineItems = subscribersAndLineItems.GetRange(1, subscribersAndLineItems.Count - 1),
                    //need to add +1 below too! Man the price we pay for stripping off a line break earlier
                    LineMarkers = Enumerable.Range(startClaimMarker, Regex.Matches(fullSegment, @"(\r\n)").Count+1).ToList(),
                    Index = i
                });

                HL20Segment currSegment = subSegments.Last();

                //need to create the 837 matrix dictionary for line numbers and segments
                currSegment.Line837Matrix = currSegment.create837Matrix(currSegment.SegmentText, currSegment.LineMarkers);
                subSegments.Last().Line837Matrix = currSegment.Line837Matrix;

                //need to add one to claim line number to account for the line break fix as well
                startClaimMarker += Regex.Matches(fullSegment, @"(\r\n)").Count +1;
                if (otherSubscriber)
                {
                    string otherSubscriberJoin = "";
                    for (int j = 2; j < (subscribers.Count)-1; j++)
                    {
                        otherSubscriberJoin = subscribers.ElementAt(j) + subscribers.ElementAt(j + 1);
                        subSegments.ElementAt(i).OtherSubscribers.Add(otherSubscriberJoin);
                        j++;
                    }
                }
                k = k + 2;

                //TODO: add logic for extra CLM segments adding with subsequent line markers
                if (extraHLs.Count != 0)
                {
                    //i++;
                    foreach(var extra in extraHLs)
                    {
                        //need to take final line break off the last segment to match line numbers, small bug
                        extra.SegmentText = extra.SegmentText.TrimEnd('\r', '\n');
                        extra.LineMarkers = Enumerable.Range(startClaimMarker, Regex.Matches(extra.SegmentText, @"(\r\n)").Count + 1).ToList();
                        extra.Line837Matrix = extra.create837Matrix(extra.SegmentText, extra.LineMarkers);
                        //need to add one to claim line number to account for the line break fix as well
                        startClaimMarker += Regex.Matches(extra.SegmentText, @"(\r\n)").Count + 1;
                        extra.ErrorText += "MISSING HL! Check breaking preferences/interface...";
                        extra.Errors.Add("MISSING HL!!!");
                        extra.Index = i;
                        subSegments.Add(extra);
                        //i++;
                    }
                }
            }

            envelope.Header = rawANSIArray[0];
            envelope.BatchType = determineBatchType(envelope.Header);
            envelope.Footer = footer;
            //envelope.Footer = rawANSIArray[rawANSIArray.Length - 2];

            return Tuple.Create(envelope, subSegments, delimiter);

        }

        /// <summary>
        /// This is the method that the program uses to automatically determine a breaking preference
        /// It will return a blank string if none of the CLM segments match the HL segment counts
        /// Anyone should be able to manually break the file if they know the client settings/ know what they're doing
        /// </summary>
        /// <param name="claimFilePath"></param>
        /// <returns>breakingSegment, segmentTotals</returns>
        public static Tuple<string,Dictionary<string,int>, string> selectHLBreakPreference(string claimFilePath)
        {
            StreamReader reader = File.OpenText(claimFilePath);

            string rawANSI = reader.ReadToEnd();

            reader.Close();

            string delimiter = Regex.Match(rawANSI, @"(?<=ISA)([\s\S])").ToString();

            Dictionary<string,int> breakingDictionary = new Dictionary<string, int>();

            int numberOfCLMs = Regex.Matches(rawANSI, @"CLM\" + delimiter + @"[0-9]*(?=.*~?\r?\n?)").Count;

            string breakingSegment = "";

            Dictionary<string, int> segmentTotals = new Dictionary<string, int>();

            breakingDictionary.Add(@"(HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"20\" + delimiter + @".[~\r\n]+)", Regex.Matches(rawANSI, @"(HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"20\" + delimiter + @".[~\r\n]+)").Count);

            breakingDictionary.Add(@"(HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"22\" + delimiter + @".[~\r\n]+)", Regex.Matches(rawANSI, @"(HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"22\" + delimiter + @".[~\r\n]+)").Count);

            breakingDictionary.Add(@"(HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"23\" + delimiter + @".[~\r\n]+)", Regex.Matches(rawANSI, @"(HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"23\" + delimiter + @".[~\r\n]+)").Count);

            segmentTotals.Add("CLM Segments", numberOfCLMs);
            segmentTotals.Add("HL 20 Segments", breakingDictionary.ElementAt(0).Value);
            segmentTotals.Add("HL 22 Segments", breakingDictionary.ElementAt(1).Value);
            segmentTotals.Add("HL 23 Segments", breakingDictionary.ElementAt(2).Value);

            foreach (var item in breakingDictionary)
            {
                if(numberOfCLMs == item.Value)
                {
                    breakingSegment = item.Key;
                    return Tuple.Create(breakingSegment,segmentTotals,delimiter);
                }
            }

            //if the above foreach loop fails to match an HL segment with the number of segments, returning the blank string should be interpreted as an error
            return Tuple.Create(breakingSegment,segmentTotals,delimiter);

        }



        /// <summary>
        /// This is the method that validates the segments, currently by HL20/Billing Provider breaks/2000A
        /// Will probably add method overrides for the other segment break types later
        /// </summary>
        /// <param name="validatingSegments"></param>
        /// <returns>validatingSegments</returns>
        public static ObservableCollection<HL20Segment> validateSegments(ObservableCollection<HL20Segment> validatingSegments, string delimiter, Tuple<string,char> batchType)
        {

            foreach (HL20Segment item in validatingSegments)
            {
                if (Regex.Match(item.BillingProvider, @"\r\n(?:(?![NM1]).*[~\r\n]+N3)").Success)
                {
                    item.ErrorText += "2000A: Missing NM1 Segment Line: " + matchLineNumber(item, Regex.Match(item.BillingProvider, @"(?<=\r\n(?![NM1]).*[~\r\n]+)(N3.*[~\r\n]+)").Value, 0) + "\r\n";
                    item.Errors.Add("2000A: Missing NM1 Segment");
                }

                //we should only check for the subscriber if we know the claim segment is whole
                if(!item.Errors.Contains("MISSING HL!!!"))
                    //begin 2000B subscriber checks
                    checkSubscriberSegments(delimiter, item);

                //begin 2300 level checks (DTP, HI, CLM)
                check2300Segments(delimiter, item);

                //only check for other subscribers if the claim segment is whole
                if (!item.Errors.Contains("MISSING HL!!!"))
                    //check our other subscribers
                    checkOtherSubscribers(delimiter, item);

                //check our line items
                check2400Segments(delimiter, batchType, item);

                if (item.Errors.Count == 0)
                {
                    item.Errors.Add("(No Errors)");
                }
            }

            return validatingSegments;
        }

        private static void check2400Segments(string delimiter, Tuple<string, char> batchType, HL20Segment item)
        {
            for (int i = 0; i < item.LineItems.Count; i++)
            {
                if (Regex.Match(item.LineItems[i], @"\r\n(?:(?![NM1]).*[~\r\n]+N3)").Success)
                {
                    item.ErrorText += "2420: Missing NM1 Segment [" + (i + 1) + "] Line: " + matchLineNumber(item, Regex.Match(item.LineItems[i], @"\r\n(?:(?![NM1]).*[~\r\n]+N3)").Value, 0) + "\r\n";
                    item.Errors.Add("2420: Missing NM1 Segment [" + (i + 1) + "]");
                }
                if (Regex.Match(item.LineItems[i], @"(?:(?<=LX\" + delimiter + @"))\d+(?=[~\r\n])").Value != (i + 1).ToString())
                {
                    item.ErrorText += "2400 LX: Improper LX Segment/Out of Order [" + (i + 1) + "] Line: " + matchLineNumber(item, Regex.Match(item.LineItems[i], @"LX\" + delimiter + @"\d+[~\r\n]").Value, 0) + "\r\n";
                    item.Errors.Add("2400 LX: Improper LX Segment/Out of Order [" + (i + 1) + "]");
                }
                if (Regex.Match(item.LineItems[i], @"DTP\" + delimiter + @"\" + delimiter + @".*[:~\r\n]").Success)
                {
                    item.ErrorText += "2400 DTP: Missing Identifier Field [" + (i + 1) + "] Line: " + matchLineNumber(item, Regex.Match(item.LineItems[i], @"DTP\" + delimiter + @"\" + delimiter + @".*[:~\r\n]").Value, 0) + "\r\n";
                    item.Errors.Add("2400 DTP: Missing Identifier Field [" + (i + 1) + "]");
                }
                if (Regex.Match(item.LineItems[i], @"REF\" + delimiter + @"\" + delimiter + @".*[:~\r\n]").Success)
                {
                    item.ErrorText += "2400 REF: Missing Identifier Field [" + (i + 1) + "] Line: " + matchLineNumber(item, Regex.Match(item.LineItems[i], @"REF\" + delimiter + @"\" + delimiter + @".*[:~\r\n]").Value, 0) + "\r\n";
                    item.Errors.Add("2400 REF: Missing Identifier Field [" + (i + 1) + "]");
                }
                //if the batch is an institutional one that is chargeable
                if (batchType.Item1.Contains("837I") && batchType.Item2 == 'C')
                {
                    //TODO: insert validation logic, will start with missing revenue codes
                    if (!Regex.Match(item.LineItems[i], @"\r\nSV2\" + delimiter + @"\d{4}\" + delimiter).Success)
                    {
                        item.ErrorText += "2400 SV2: Segment Missing [" + (i + 1) + "] Line: " + matchLineNumber(item, Regex.Match(item.LineItems[i], @"LX\" + delimiter + @"\d+[~\r\n]").Value, 0) + "\r\n";
                        item.Errors.Add("2400 SV2: Segment Missing [" + (i + 1) + "]");
                    }
                    else if (!Regex.Match(item.LineItems[i], @"SV2\" + delimiter + @"\d{4}\" + delimiter).Success)
                    {
                        item.ErrorText += "2400 SV2: Missing Revenue Code [" + (i + 1) + "] Line: " + matchLineNumber(item, Regex.Match(item.LineItems[i], @"SV2\" + delimiter + @".*[:~\r\n]").Value, 0) + "\r\n";
                        item.Errors.Add("2400 SV2: Missing Revenue Code [" + (i + 1) + "]");
                    }
                }
            }
        }

        private static void checkOtherSubscribers(string delimiter, HL20Segment item)
        {
            int otherSubs = 0;
            foreach (var subscriber in item.OtherSubscribers)
            {
                otherSubs++;
                string firstLine = new StringReader(subscriber).ReadLine();
                if (!Regex.Match(subscriber, @"NM1\" + delimiter + @"IL.*?[~\r\n]+").Success)
                {
                    item.ErrorText += "2330A: Missing NM1 Segment [" + otherSubs + "] Line: " + matchLineNumber(item, firstLine, 0) + "\r\n";
                    item.Errors.Add("2330A: Missing NM1 Segment [" + otherSubs + "]");
                }
                if (!Regex.Match(subscriber, @"NM1\" + delimiter + @"PR.*?[~\r\n]+").Success)
                {
                    item.ErrorText += "2330B: Missing NM1 Segment [" + otherSubs + "] Line: " + matchLineNumber(item, firstLine, 0) + "\r\n";
                    item.Errors.Add("2330B: Missing NM1 Segment [" + otherSubs + "]");
                }
                if (!Regex.Match(subscriber, @"OI.*?[~\r\n]+").Success)
                {
                    item.ErrorText += "2320: Missing OI Segment [" + otherSubs + "] Line: " + matchLineNumber(item, firstLine, 0) + "\r\n";
                    item.Errors.Add("2320: Missing OI Segment [" + otherSubs + "]");
                }
            }
        }

        private static void check2300Segments(string delimiter, HL20Segment item)
        {
            if (Regex.Match(item.Claim2300, @"\r\n(?:(?![NM1]).*[~\r\n]+N3)").Success)
            {
                var N2300Found = Regex.Match(item.Claim2300, @"\r\n(?:(?![NM1]).*[~\r\n]+N3)").Value.Trim('\r', '\n');
                var N2300SplitCheck = Regex.Split(N2300Found, @"\r\n");
                if (Regex.Match(N2300SplitCheck[0], @"OI\*").Success || Regex.Match(N2300SplitCheck[0], @"CAS\*").Success || Regex.Match(N2300SplitCheck[0], @"AMT\*").Success)
                {
                    item.ErrorText += "2330: Missing NM1 Segment Line: " + matchLineNumber(item, N2300Found, 0) + "\r\n";
                    item.Errors.Add("2330: Missing NM1 Segment");
                }
                else
                {
                    item.ErrorText += "2310: Missing NM1 Segment Line: " + matchLineNumber(item, N2300Found, 0) + "\r\n";
                    item.Errors.Add("2310: Missing NM1 Segment");
                }
            }
            //we're doing field checks on all eligible HI segments at this level
            MatchCollection HI2300s = Regex.Matches(item.Claim2300, @"(?<=[~\r\n])HI\" + delimiter + @".*[~\r\n]");
            int HITypeIdx = 1;
            int HI02Idx = 1;
            int D8Idx = 1;
            foreach (Match HIline in HI2300s)
            {
                //if we don't have a type of HI in the segment, there's probably no point in checking the rest (BE/BH/DTP)
                if(!Regex.Match(HIline.Value, @"HI\" + delimiter + @"\w+[\" + delimiter + @":~\r\n]").Success)
                {
                    item.ErrorText += "2300 HI: Missing Type Field [" + HITypeIdx + "] Line: " + matchLineNumber(item, HIline.Value, HITypeIdx) + "\r\n";
                    item.Errors.Add("2300 HI: Missing Type Field [" + HITypeIdx + "]");
                    HITypeIdx++;
                }
                //otherwise it's time to check the field codes inside
                else
                {
                    //getting the type of an HI segment can help narrow things further
                    string HIType = Regex.Match(HIline.Value, @"(?<=HI\" + delimiter + @")\w+(?=[\" + delimiter + @":~\r\n]+)").Value;
                    if (!Regex.Match(HIline.Value, @"HI\" + delimiter + @"\w+:[^:]+[:~\r\n]").Success)
                    {
                        item.ErrorText += "2300 HI: HI02 Code Missing (" + HIType + ") [" + HI02Idx + "] Line: " + matchLineNumber(item, HIline.Value, 0) + "\r\n";
                        item.Errors.Add("2300 HI: HI02 Code Missing (" + HIType + ") [" + HI02Idx + "]");
                        HI02Idx++;
                    }
                    //we need to check for a paired value if there's a D8 code in the segment
                    if(Regex.Match(HIline.Value, @"D8^[:d+:]").Success)
                    {
                        item.ErrorText += "2300 HI: D8 Code With No Value [" + D8Idx + "] Line: " + matchLineNumber(item, HIline.Value, D8Idx) + "\r\n";
                        item.Errors.Add("2300 HI: D8 Code With No Value [" + D8Idx + "]");
                        D8Idx++;
                    }
                }
            }

            //now we're doing field checks on all eligible DTP segments at this level
            MatchCollection DTP2300s = Regex.Matches(item.Claim2300, @"(?<=[~\r\n])DTP\" + delimiter + @".*[~\r\n]");
            int DTPIdx = 1;
            foreach (Match DTPline in DTP2300s)
            {
                //check if there's an identifier for the DTP
                if (!Regex.Match(DTPline.Value, @"DTP\" + delimiter + @".+\" + delimiter +  @".*[:~\r\n]").Success)
                {
                    item.ErrorText += "2300 DTP: Missing Identifier Field [" + DTPIdx + "] Line: " + matchLineNumber(item, DTPline.Value, DTPIdx) + "\r\n";
                    item.Errors.Add("2300 DTP: Missing Identifier Field [" + DTPIdx + "]");
                    DTPIdx++;
                }
            }
        }

        private static void checkSubscriberSegments(string delimiter, HL20Segment item)
        {
            string subFirstLine = new StringReader(item.SubscriberSegment).ReadLine();
            //this is checking to see if we have a 2000C segment, but no 2000B
            if (!Regex.Match(item.SegmentText, @"HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"22\" + delimiter + @"\d*[~\r\n]+").Success && Regex.Match(item.SegmentText, @"HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"23\" + delimiter + @"\d*[~\r\n]+").Success)
            {
                item.ErrorText += "2000B: Missing HL Segment (No 2000B for 2000C/HL 22 for HL 23) Line: " + matchLineNumber(item, Regex.Match(item.SegmentText, @"HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"23\" + delimiter + @"\d*[~\r\n]+").Value, 0) + "\r\n";
                item.Errors.Add("2000B: Missing HL Segment (No 2000B for 2000C/HL 22 for HL 23)");
            }
            if (Regex.Match(item.SubscriberSegment, @"HL\" + delimiter + @"\d*\" + delimiter + @"\d*\" + delimiter + @"22\" + delimiter + @"\d*[~\r\n]+(?:(?![SBR]).*[~\r\n]+)").Success)
            {
                item.ErrorText += "2000B: Missing SBR Segment Line: " + matchLineNumber(item, Regex.Match(item.SubscriberSegment, @"HL\" + delimiter + @"\d*\" + delimiter + @"22\" + delimiter + @"\d*[~\r\n]+(?:(?![SBR]).*[~\r\n]+)").Value, 0) + "\r\n";
                item.Errors.Add("2000B: Missing SBR Segment");
            }
            
            if (!Regex.Match(item.SubscriberSegment, @"NM1\" + delimiter + @"IL.*?[~\r\n]+").Success)
            {
                item.ErrorText += "2010BA: Missing NM1 Segment Line: " + matchLineNumber(item, subFirstLine, 0) + "\r\n";
                item.Errors.Add("2010BA: Missing NM1 Segment");
            }

            if (!Regex.Match(item.SubscriberSegment, @"CLM\" + delimiter + @"[\d-]*\" + delimiter + @"+.*?[~\r\n]+").Success)
            {
                item.ErrorText += "2300: Missing CLM Segment Line: " + matchLineNumber(item, subFirstLine, 0) + "\r\n";
                item.Errors.Add("2300: Missing CLM Segment");
            }

            if (!Regex.Match(item.SubscriberSegment, @"NM1\" + delimiter + @"PR.*?[~\r\n]+").Success)
            {
                item.ErrorText += "2010BB: Missing NM1 Segment Line: " + matchLineNumber(item, subFirstLine, 0) + "\r\n";
                item.Errors.Add("2010BB: Missing NM1 Segment");
            }

            if (!Regex.Match(item.SubscriberSegment, @"HI\" + delimiter + @"ABK.*?[~\r\n]+").Success && Regex.Match(item.SubscriberSegment, @"HI\" + delimiter + @"A[^BK].*?[~\r\n]+").Success)
            {
                item.ErrorText += "2300 HI: Missing Principal DX Code Line: " + matchLineNumber(item, subFirstLine, 0) + "\r\n";
                item.Errors.Add("2300 HI: Missing Principal DX Code");
            }

            if (Regex.Match(item.SubscriberSegment, @"\r\nOI.*?[~\r\n]+").Success)
            {
                item.ErrorText += "2320: Missing SBR Segment Line: " + matchLineNumber(item, Regex.Match(item.SubscriberSegment, @"\r\nOI.*?[~\r\n]+").Value, 0) + "\r\n";
                item.Errors.Add("2320: Missing SBR Segment");
            }
        }

        public static Tuple<string,char> determineBatchType(string headerSegment)
        {
            if(Regex.Match(headerSegment, @"\r\nGS[\s\S]*?X222").Success)
            {
                return Tuple.Create("837P (Professional Batch)", 'C');
            }
            else if (Regex.Match(headerSegment, @"\r\nGS[\s\S]*?X223").Success)
            {
                if (Regex.Match(headerSegment, @"\r\nBHT[\s\S]*?RP").Success)
                {
                    return Tuple.Create("837I (State Report)", 'R');
                }
                else {
                    return Tuple.Create("837I (Institutional Batch)", 'C');
                }
            }
            else if (Regex.Match(headerSegment, @"\r\nGS[\s\S]*?X225").Success)
            {
                return Tuple.Create("837 State Report Batch (5010)", 'R');
            }
            else
            {
                return Tuple.Create("Not Detected", 'N');
            }
        }

        public static int matchLineNumber(HL20Segment item, string searchPattern, int idxMultiple)
        {
            int returnLineNum = 0;
            string compare = searchPattern.Trim('\r','\n');
            //we need to consider when we are comparing TWO elements in sequence because of expected mapping when the file doesn't have all the elements needed
            if(compare.Contains("\r\n"))
            {
                //first, let's break the comparing string into two elements for us to loop through the sequence
                var compareElements = Regex.Split(compare, @"\r\n");
                //initialize the start of the search list to compare
                //Dictionary<int,string> loopSearch = item.Line837Matrix;
                List<KeyValuePair<int, string>> loopSearch = new List<KeyValuePair<int, string>>();
                //we're going to loop through the array to collect possibilities
                foreach(var comparingItem in compareElements)
                {
                    var curSelect = item.Line837Matrix.Where(a => a.Value.StartsWith(comparingItem)).ToList();
                    loopSearch.AddRange(curSelect);
                }
                //we don't have enough unique elements - we need to find the elements closest to each other
                if(loopSearch.Count > compareElements.Count())
                {
                    int CompareKey(KeyValuePair<int, string> a, KeyValuePair<int, string> b)
                    {
                        return a.Key.CompareTo(b.Key);
                    }

                    loopSearch.Sort(CompareKey);

                    bool groupFound = false;
                    while(!groupFound)
                    {
                        int idx = loopSearch.First().Key;

                        var possibleReturn = loopSearch.TakeWhile(a => (a.Key - idx) < compareElements.Count()).ToList();

                        if (possibleReturn.Count() == compareElements.Count())
                            groupFound = true;
                        else
                            loopSearch.RemoveRange(0, possibleReturn.Count());
                    }
                    List<int> keys = (from a in loopSearch select a.Key).ToList();
                    //we have finally found the elements needed, and can safely return the lowest key from the new list
                    returnLineNum = keys.Min();
                }
                //we have found enough unique elements
                else
                {
                    List<int> keys = (from a in loopSearch select a.Key).ToList();
                    returnLineNum = keys.Min();
                }
            }
            else if(idxMultiple > 0)
            {
                var selected = item.Line837Matrix.Where(a => a.Value == compare).ToList();
                //this should fix the more immediate issue, but I have a gut feeling something else might come up.
                try
                {
                    if (selected.Count > 1)
                        returnLineNum = selected.ElementAt(idxMultiple - 1).Key;
                    //if we only have one line element, just grab that one element from the list
                    else
                        returnLineNum = selected.ElementAt(0).Key;
                }
                catch(Exception e)
                {
                    throw new Exception("Attempted to compare internal HI segment elements, but failed. For the developer: " + e.Message);
                }
            }
            else
            {
                var selected = item.Line837Matrix.Where(a => a.Value == compare).ToList();
                if (selected.Count == 1)
                {
                    returnLineNum = selected[0].Key;
                }
                else
                {
                    returnLineNum = selected[selected.Count - 1].Key;
                }
            }
            return returnLineNum;
        }
    }
}
