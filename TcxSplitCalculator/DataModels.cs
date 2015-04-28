using Aga.Controls.Tree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace TcxSplitCalculator
{
    public interface ITcxModel
    {
        string Name { get; }
        double Pace { get; }
        string PaceString { get; }
        double Distance { get; }
        TimeSpan ElapsedTime { get; }
        string ElapsedTimeString { get; }
    }

    public interface ITcxModelContainer : ITcxModel
    {
        IEnumerable<ITcxModel> GetItems();
    }

    public class TcxModel : ITreeModel
    {
        private readonly List<TcxActivity> _activities;
        public List<TcxActivity> Activities
        {
            get { return _activities; }
        }

        public TcxModel(string filename, TcxDistanceOption distanceOption, double calcLapDist)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(filename);
            XmlNamespaceManager nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("tcx", "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2");

            _activities = new List<TcxActivity>();
            // For right now, only single activity files are allowed
            foreach (XmlNode activityNode in doc.SelectNodes("//tcx:TrainingCenterDatabase/tcx:Activities/tcx:Activity", nsMgr))
            {
                _activities.Add(new TcxActivity(activityNode, nsMgr, distanceOption, calcLapDist));
            }
        }

        public IEnumerable GetChildren(object parent)
        {
            if(parent == null)
            {
                return _activities;
            }
            else if(parent is ITcxModelContainer)
            {
                return (parent as ITcxModelContainer).GetItems();
            }
            else
            {
                return null;
            }
        }

        public bool HasChildren(object parent)
        {
            if (parent == null)
            {
                return _activities.Count > 0;
            }
            else if (parent is ITcxModelContainer)
            {
                return (parent as ITcxModelContainer).GetItems().Count() > 0;
            }
            else
            {
                return false;
            }
        }
    }

    public class TcxActivity : ITcxModelContainer
    {
        private readonly List<TcxLap> _laps;
        public List<TcxLap> Laps
        {
            get
            {
                return _laps;
            }
        }

        public TcxActivity(XmlNode activityNode, XmlNamespaceManager nsMgr, TcxDistanceOption distanceOption, double calcLapDist)
        {
            _laps = new List<TcxLap>();
            double lastLapEndDistance = 0;
            foreach (XmlNode lapNode in activityNode.SelectNodes("./tcx:Lap", nsMgr))
            {
                var lap = new TcxLap(lapNode, nsMgr, distanceOption, calcLapDist, lastLapEndDistance);
                lastLapEndDistance = lap.LastLapEndDistance;
                _laps.Add(lap);
            }
        }

        public bool HasChildren(object parent)
        {
            return _laps.Count > 0;
        }

        public string Name
        {
            get { return "Activity"; }
        }

        public IEnumerable<ITcxModel> GetItems()
        {
            return _laps;
        }

        public double Pace
        {
            get { return _laps.Average(x => x.Pace); }
        }

        public string PaceString
        {
            get 
            {
                double pace = Pace;
                int intPart = (int)Math.Truncate(pace);
                double fractionPart = pace - Math.Truncate(pace);
                int seconds = (int)(fractionPart * 60);
                return string.Format("{0}:{1}{2}", intPart, (seconds < 10 ? "0" : ""), seconds);
            }
        }

        public double Distance
        {
            get { return _laps.Sum(x => x.Distance); }
        }

        public TimeSpan ElapsedTime
        {
            get
            {
                TimeSpan sum = new TimeSpan(0, 0, 0);
                foreach (var span in _laps.Select(x => x.ElapsedTime))
                    sum = sum.Add(span);
                return sum;
            }
        }

        public string ElapsedTimeString
        {
            get { return ElapsedTime.ToString("%h\\:mm\\:ss\\.fff"); }
        }
    }

    public class TcxLap : ITcxModelContainer
    {
        public double LastLapEndDistance { get; private set; }

        public DateTime StartTime { get; private set; }
        public double TotalSeconds { get; private set; }
        public double DistanceMeters { get; private set; }
        public double MaxSpeed { get; private set; }
        public double CaloriesBurned { get; private set; }
        private readonly TcxDistanceOption _distOption;
         
        private readonly List<TcxTrack> _tracks;
        public List<TcxTrack> Tracks
        {
            get
            {
                return _tracks;
            }
        }

        public TcxLap(XmlNode lapNode, XmlNamespaceManager nsMgr, TcxDistanceOption distanceOption, double calcLapDist, double lastLapEndDistance)
        {
            _distOption = distanceOption;
            _tracks = new List<TcxTrack>();
            LastLapEndDistance = lastLapEndDistance;
            foreach (XmlNode trackNode in lapNode.SelectNodes("./tcx:Track", nsMgr))
            {
                var track = new TcxTrack(trackNode, nsMgr, distanceOption, calcLapDist, LastLapEndDistance);
                LastLapEndDistance = track.EndDistance;
                _tracks.Add(track);
            }

            StartTime = DateTime.ParseExact(lapNode.Attributes["StartTime"].Value, "yyyy-MM-ddTHH:mm:ss.FFFZ", CultureInfo.InvariantCulture);
            TotalSeconds = double.Parse(lapNode["TotalTimeSeconds"].InnerText);
            DistanceMeters = double.Parse(lapNode["DistanceMeters"].InnerText);
            MaxSpeed = double.Parse(lapNode["MaximumSpeed"].InnerText);
            CaloriesBurned = double.Parse(lapNode["Calories"].InnerText);
        }

        public bool HasChildren(object parent)
        {
            return _tracks.Count > 0;
        }

        public string Name
        {
            get { return StartTime.ToString(); }
        }

        public IEnumerable<ITcxModel> GetItems()
        {
            return _tracks;
        }

        public double Pace
        {
            get
            {
                return ElapsedTime.TotalMinutes / Distance;
            }
        }

        public string PaceString
        {
            get
            {
                double pace = Pace;
                int intPart = (int)Math.Truncate(pace);
                double fractionPart = pace - Math.Truncate(pace);
                int seconds = (int)(fractionPart * 60);
                return string.Format("{0}:{1}{2}", intPart, (seconds < 10 ? "0" : ""), seconds);
            }
        }

        public double Distance
        {
            get 
            {
                return _distOption == TcxDistanceOption.Miles 
                    ? CalculationHelpers.MetersToMiles(DistanceMeters)
                    : CalculationHelpers.MetersToKilometers(DistanceMeters);
            }
        }

        public TimeSpan ElapsedTime
        {
            get
            {
                return TimeSpan.FromSeconds(TotalSeconds);
            }
        }

        public string ElapsedTimeString
        {
            get
            {
                return ElapsedTime.ToString("%h\\:mm\\:ss\\.fff");
            }
        }
    }

    public class TcxTrack : ITcxModelContainer
    {
        public double EndDistance { get; private set;}

        private readonly List<TcxCalculatedLap> _trackpoints;
        public List<TcxCalculatedLap> Trackpoints
        {
            get
            {
                return _trackpoints;
            }
        }

        public TcxTrack(XmlNode trackNode, XmlNamespaceManager nsMgr, TcxDistanceOption distanceOption, double calcLapDist, double lastLapEndDistance)
        {
            _trackpoints = new List<TcxCalculatedLap>();

            bool newLap = true;
            DateTime startTime = DateTime.MinValue;
            DateTime latestTime = DateTime.MinValue;
            double currDist = 0;
            double lastCalcLapDistEnd = 0;
            int lapCount = 1;

            foreach(XmlNode trackpointNode in trackNode.SelectNodes("./tcx:Trackpoint", nsMgr))
            {
                var trackpoint = new TcxTrackpoint(trackpointNode);
                EndDistance = trackpoint.DistanceMeters;
                if(newLap)
                {
                    startTime = trackpoint.Time;
                    currDist = 0;
                    newLap = false;
                }
                else
                {
                    latestTime = trackpoint.Time;
                    currDist = distanceOption == TcxDistanceOption.Miles
                        ? CalculationHelpers.MetersToMiles((trackpoint.DistanceMeters - lastLapEndDistance) - lastCalcLapDistEnd)
                        : CalculationHelpers.MetersToKilometers((trackpoint.DistanceMeters - lastLapEndDistance) - lastCalcLapDistEnd);

                    if(currDist >= calcLapDist)
                    {
                        lastCalcLapDistEnd = trackpoint.DistanceMeters;
                        if (lastLapEndDistance > 0)
                            lastLapEndDistance = 0;
                        TimeSpan lapTime = latestTime - startTime;
                        _trackpoints.Add(new TcxCalculatedLap(string.Format("Lap {0}", lapCount++), currDist, lapTime));
                        newLap = true;
                    }
                }
            }

            if(!newLap && currDist > 0)
            {
                // we might have part of a calculated lap
                TimeSpan lapTime = latestTime - startTime;
                _trackpoints.Add(new TcxCalculatedLap(string.Format("Lap {0}", lapCount++), currDist, lapTime));
            }
        }

        public IEnumerable GetChildren(object parent)
        {
            return _trackpoints;
        }

        public bool HasChildren(object parent)
        {
            return _trackpoints.Count > 0;
        }

        public string Name
        {
            get { return "Track"; }
        }

        public IEnumerable<ITcxModel> GetItems()
        {
            return _trackpoints;
        }

        public double Pace
        {
            get { return _trackpoints.Average(x => x.Pace); }
        }

        public string PaceString
        {
            get 
            {
                double pace = Pace;
                int intPart = (int)Math.Truncate(pace);
                double fractionPart = pace - Math.Truncate(pace);
                int seconds = (int)(fractionPart * 60);
                return string.Format("{0}:{1}{2}", intPart, (seconds < 10 ? "0" : ""), seconds);
            }
        }

        public double Distance
        {
            get { return _trackpoints.Sum(x => x.Distance); }
        }

        public TimeSpan ElapsedTime
        {
            get 
            {
                TimeSpan sum = new TimeSpan(0, 0, 0);
                foreach (var span in _trackpoints.Select(x => x.ElapsedTime))
                    sum = sum.Add(span);
                return sum;
            }
        }

        public string ElapsedTimeString
        {
            get { return ElapsedTime.ToString("%h\\:mm\\:ss\\.fff"); }
        }
    }

    public class TcxCalculatedLap : ITcxModel
    {
        private readonly string _name;
        private readonly double _distance;
        private readonly TimeSpan _interval;

        public TcxCalculatedLap(string name, double distance, TimeSpan interval)
        {
            _name = name;
            _distance = distance;
            _interval = interval;
        }

        public string Name
        {
            get { return _name; }
        }

        public double Pace
        {
            get { return _interval.TotalMinutes / Distance; }
        }

        public string PaceString
        {
            get 
            {
                double pace = Pace;
                int intPart = (int)Math.Truncate(pace);
                double fractionPart = pace - Math.Truncate(pace);
                int seconds = (int)(fractionPart * 60);
                return string.Format("{0}:{1}{2}", intPart, (seconds < 10 ? "0" : ""), seconds);
            }
        }

        public double Distance
        {
            get { return _distance; }
        }

        public TimeSpan ElapsedTime
        {
            get { return _interval; }
        }

        public string ElapsedTimeString
        {
            get { return ElapsedTime.ToString("%h\\:mm\\:ss\\.fff"); }
        }
    }

    public class TcxTrackpoint
    {
        public DateTime Time { get; private set; }
        public double Latitude { get; private set; }
        public double Longitude { get; private set; }
        public double DistanceMeters { get; private set; }

        public TcxTrackpoint(XmlNode trackpointNode)
        {
            Time = DateTime.ParseExact(trackpointNode["Time"].InnerText, "yyyy-MM-ddTHH:mm:ss.FFFZ", CultureInfo.InvariantCulture);
            if (trackpointNode["DistanceMeters"] != null)
                DistanceMeters = double.Parse(trackpointNode["DistanceMeters"].InnerText);
            if (trackpointNode["Position"] != null)
            {
                Latitude = double.Parse(trackpointNode["Position"]["LatitudeDegrees"].InnerText);
                Longitude = double.Parse(trackpointNode["Position"]["LongitudeDegrees"].InnerText);
            }
        }
    }
}
