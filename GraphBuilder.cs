using Autodesk.DesignScript.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DynamoGraphBuilder
{
    enum ConnectionType
    {
        Supporting,
        SupportedBy,
        Beside,
        NotKnown


    }

    class Connection
    {
        public int ConnectedWithId { get; set; }
        public ConnectionType ConnectionType { get; set; }
        public bool IsConnectedWithStructuralElement { get; set; }


    }
    [Serializable]
    public class GraphElementView
    {
        public int Id { get; set; }
        public bool IsStructural { get; set; }
        public string Category { get; set; }
        public string Material { get; set; }
        public string GetColor(int perfectConnNum, int goodConnecNum)
        {
            var totDepElements = AllDependentElementsIds.Count();
            if (totDepElements < perfectConnNum)
                return "#008000";
            if (totDepElements <= goodConnecNum)
                return "#FFFF00";
            return "#FF0000";
        }
        public string Volume { get; set; }

        public List<int> DirectConnectedElementsIds { get; set; }
        public List<int> NonSupportingDirectElementsIds { get; set; }
        public List<int> DirectSupportedElementsIds { get; set; }
        public List<int> DirectConnectedStructuralElementsIds { get; set; }

        public List<int> NonDirectDependentElementsIds { get; set; }
        public List<int> AllDependentElementsIds { get; set; }

    }
    class GraphElement
    {
        public int Id { get; set; }
        public List<Geometry> Geometries { get; set; }
        public BoundingBox BoundingBox { get; set; }
        private List<Connection> _connections = new List<Connection>();

        public IEnumerable<Connection> GetConnections()
         => _connections;
        public void AddConnection(Connection connection)
        => _connections.Add(connection);

        public void ClearAllConnection() { _connections.Clear(); _nonDirctDependentElmnsIds.Clear(); }

        public bool IsStructural { get; set; }
        public string Category { get; set; }
        public void AddNonDirectDependentElementId(int id) => _nonDirctDependentElmnsIds.Add(id);
        public void AddNonDirectDependentElementId(IEnumerable<int> ids)
        {
            foreach (var id in ids)
                AddNonDirectDependentElementId(id);
        }
        public string Material { get; set; }

        public string Volume { get; set; }
        public IEnumerable<int> GetDirectConnectedElementsIds() => _connections.Select(e => e.ConnectedWithId);

        public IEnumerable<int> GetDirectConnectedStructuralElementsIds() => _connections.Where(c => c.IsConnectedWithStructuralElement).Select(e => e.ConnectedWithId);

        public IEnumerable<int> GetNonSupportingDirectElementsIds() => _connections.Where(c => c.ConnectionType != ConnectionType.SupportedBy).Select(e => e.ConnectedWithId);

        //   public IEnumerable<int> GetDirectSupportingElementsIds() => _connections.Where(c => c.ConnectionType == ConnectionType.SupportedBy).Select(e => e.ConnectedWithId);

        public IEnumerable<int> GetDirectSupportedElementsIds() => _connections.Where(c => c.ConnectionType == ConnectionType.Supporting).Select(e => e.ConnectedWithId);
        public IEnumerable<int> GetNonDirectDependentElementsIds() => _nonDirctDependentElmnsIds.Distinct();
        public IEnumerable<int> GetAllDependentElementsIds()
     => _nonDirctDependentElmnsIds.Union(GetDirectSupportedElementsIds());

        private HashSet<int> _nonDirctDependentElmnsIds = new HashSet<int>();

    }
    public class GraphConfiguration
    {
        public static GraphConfiguration Create(int perfectDepdNum, int goodDepNum, bool floorSupportsAnyAboveColumn
            , bool colorRevitModel, bool clearCashedRevitData, GraphColoringMethod coloringMethod)
               => new GraphConfiguration()
               {
                   PerfectDepdNum = perfectDepdNum,
                   GoodDepNum = goodDepNum,
                   ClearCashedRevitData = clearCashedRevitData,
                   ColoringMethod = coloringMethod,
                   ColorRevitModel = colorRevitModel,
                   FloorSupportsAnyAboveColumn = floorSupportsAnyAboveColumn
               };
        public int PerfectDepdNum { get; set; }
        public int GoodDepNum { get; set; }
        public bool FloorSupportsAnyAboveColumn { get; set; }
        public bool ColorRevitModel { get; set; }
        public bool ClearCashedRevitData { get; set; }
        public GraphColoringMethod ColoringMethod { get; set; }

    }

    public enum GraphColoringMethod
    {
        AllDependentElements,
        DirectSupportedElements,
        NonDirectDependentElements,
        DirectConnectedElements,
        DirectConnectedStructuralElements,
        NonSupportingDirectElements
    }
    public static class GraphBuilder
    {
        //static int _perfectNumberOfDependecies = -1;
        //static int _goodNumberOfDependecies = -1;
        //static GraphColoringMethod _coloringMethod;
        //static bool _floorSupportsAboveColumns;

        static GraphConfiguration _graphConfig;
        const string columnCategoryFilter = "Column";
        const string wallCategoryFilter = "Wall";
        const string foundationCategoryFilter = "Foundation";
        const string beamsCategoryFilter = "Framing";
        const string floorCategoryFilter = "Floor";


        static Dictionary<int, GraphElement> _cahsedGraphElements = new Dictionary<int, GraphElement>();
        static void CashGraphElements(List<Revit.Elements.Element> allBuildingElements)
        {
            if (_cahsedGraphElements.Count == 0)
                allBuildingElements.ForEach(e => _cahsedGraphElements.Add(e.Id, new GraphElement() { Id = e.Id, IsStructural = e.IsStructural(), Category = e.GetCategory.Name, Geometries = e.Geometry().Cast<Geometry>().ToList(), BoundingBox = e.BoundingBox, Volume = GetParameterValueByName(e, "Volume") }));
        }

        static int RemoveNonGemetricElements(this List<Revit.Elements.Element> elementsList)
            => elementsList.RemoveAll(e => e.ElementType == null);

        public static Revit.Elements.Element ElementById(List<Revit.Elements.Element> allBuildingElements, int Id)
             => allBuildingElements.FirstOrDefault(e => e.Id == Id);
        public static List<Revit.Elements.Element> ElementsByIds(List<Revit.Elements.Element> allBuildingElements, List<int> Ids)
             => Ids.Select(i => ElementById(allBuildingElements, i)).ToList();


        //static void IntersectedElements(Revit.Elements.Element baseElement, List<Revit.Elements.Element> buildingElements)
        //{
        //    buildingElements.RemoveNonGemetricElements();
        //    buildingElements.Remove(baseElement);
        //    var connectedElements = GetIntersectedElements(baseElement, buildingElements);
        //    GenerateGraph(baseElement, connectedElements.ToList());

        //}

        public static List<int> GetDirectConnectedElementIds(int elementId, List<GraphElementView> elements)
        => elements.FirstOrDefault(e => e.Id == elementId)?.DirectConnectedElementsIds;
        public static List<int> GetAllDependentElementIds(int elementId, List<GraphElementView> elements)
        => elements.FirstOrDefault(e => e.Id == elementId)?.AllDependentElementsIds;
        public static List<int> GetDirectSupportedElementIds(int elementId, List<GraphElementView> elements)
        => elements.FirstOrDefault(e => e.Id == elementId)?.DirectSupportedElementsIds;
        public static List<int> GetNonDirectDependentElementIds(int elementId, List<GraphElementView> elements)
        => elements.FirstOrDefault(e => e.Id == elementId)?.NonDirectDependentElementsIds;
        public static List<int> GetDirectConnectedStructuralElementsIds(int elementId, List<GraphElementView> elements)
       => elements.FirstOrDefault(e => e.Id == elementId)?.DirectConnectedStructuralElementsIds;

        public static List<int> GetNonSupportingDirectElementsIds(int elementId, List<GraphElementView> elements)
     => elements.FirstOrDefault(e => e.Id == elementId)?.NonSupportingDirectElementsIds;

        public static List<GraphElementView> WholeBuilding(List<Revit.Elements.Element> allBuildingElements, GraphConfiguration config)
        {
            _graphConfig = config;

            //_perfectNumberOfDependecies = config.PerfectDepdNum;
            //_goodNumberOfDependecies = config.GoodDepNum;
            //_coloringMethod = coloringMethod;
            //_floorSupportsAboveColumns = config.
            allBuildingElements.RemoveNonGemetricElements();
            if (_cahsedGraphElements.Count == 0 || _graphConfig.ClearCashedRevitData)
            {
                _cahsedGraphElements.Clear();
                CashGraphElements(allBuildingElements);

            }
            foreach (var cashedElmnt in _cahsedGraphElements.Values)
                cashedElmnt.ClearAllConnection();

            allBuildingElements = SortForIntersectionPriorities(ref allBuildingElements);
            BuildGraph(allBuildingElements);

            if (_graphConfig.ColorRevitModel)
                ColorRevitModel(allBuildingElements);
            return GenerateGraph(_cahsedGraphElements.Values.ToList());



        }
        static void ColorRevitModel(List<Revit.Elements.Element> allBuildingElements)
        {
            foreach (var graphElement in _cahsedGraphElements.Values)
            {
                var revitElement = allBuildingElements.First(c => c.Id == graphElement.Id);
                revitElement.OverrideColorInView(GetEquivalenRevitColor(GetGraphNodeColor(graphElement)));
            }

        }
        static void BuildGraph(List<Revit.Elements.Element> BuildingElements)
        {
            var buildingElementsCopy = new List<Revit.Elements.Element>(BuildingElements);
            for (int i = 0; i < BuildingElements.Count; i++)
            {
                buildingElementsCopy.RemoveAt(0);
                foreach (var intersectedElmnt in GetIntersectedElements(BuildingElements[i], buildingElementsCopy))
                    AssignConnection(BuildingElements[i], intersectedElmnt);
                UpdateSupportingElmnts(BuildingElements[i].Id, _cahsedGraphElements[BuildingElements[i].Id].GetAllDependentElementsIds());
            }
        }

        static List<Revit.Elements.Element> SortForIntersectionPriorities(ref List<Revit.Elements.Element> elemnts)
        {
            var columnFilter = new Func<Revit.Elements.Element, bool>(e => e.GetCategory.Name.Contains(columnCategoryFilter));
            var wallFilter = new Func<Revit.Elements.Element, bool>(e => e.IsStructural() && e.GetCategory.Name.Contains(wallCategoryFilter));
            var beamFilter = new Func<Revit.Elements.Element, bool>(e => e.GetCategory.Name.Contains(beamsCategoryFilter));
            var foundationFilter = new Func<Revit.Elements.Element, bool>(e => e.GetCategory.Name.Contains(foundationCategoryFilter));

            var sortedElments = elemnts.GetAndRemoveFromOriginal(columnFilter).ToList();
            sortedElments.AddRange(elemnts.GetAndRemoveFromOriginal(wallFilter));
            sortedElments.AddRange(elemnts.GetAndRemoveFromOriginal(beamFilter));
            sortedElments.AddRange(elemnts.GetAndRemoveFromOriginal(foundationFilter));
            sortedElments.AddRange(elemnts.GetAndRemoveFromOriginal(e => e.IsStructural()));

            sortedElments.AddRange(elemnts);

            elemnts = sortedElments;
            return sortedElments;
        }
        static IEnumerable<Revit.Elements.Element> GetAndRemoveFromOriginal(this List<Revit.Elements.Element> elemnts, Func<Revit.Elements.Element, bool> predicate)
        {
            for (int i = elemnts.Count - 1; i >= 0; i--)
                if (predicate(elemnts[i]))
                {
                    yield return elemnts[i];
                    elemnts.RemoveAt(i);
                }
        }


        static void AssignConnection(Revit.Elements.Element element1, Revit.Elements.Element element2)
        {
            var connection = GetConnection(element1, element2);
            _cahsedGraphElements[element1.Id].AddConnection(connection);
            if (connection.ConnectionType == ConnectionType.SupportedBy)
                _cahsedGraphElements[element2.Id].AddConnection(new Connection() { ConnectedWithId = element1.Id, ConnectionType = ConnectionType.Supporting, IsConnectedWithStructuralElement = element1.IsStructural() });
            else if (connection.ConnectionType == ConnectionType.Supporting)
                _cahsedGraphElements[element2.Id].AddConnection(new Connection() { ConnectedWithId = element1.Id, ConnectionType = ConnectionType.SupportedBy, IsConnectedWithStructuralElement = element1.IsStructural() });
            else if (connection.ConnectionType == ConnectionType.Beside || connection.ConnectionType == ConnectionType.NotKnown)
                _cahsedGraphElements[element2.Id].AddConnection(new Connection() { ConnectedWithId = element1.Id, ConnectionType = connection.ConnectionType, IsConnectedWithStructuralElement = element1.IsStructural() });

        }
        static void UpdateSupportingElmnts(int elementId, IEnumerable<int> newNonDirectConnectedIds)
        {
            var element = _cahsedGraphElements[elementId];
            var supElmntsConnecs = element.GetConnections().Where(c => c.ConnectionType == ConnectionType.SupportedBy);
            foreach (var supElmntCon in supElmntsConnecs)
            {
                _cahsedGraphElements[supElmntCon.ConnectedWithId].AddNonDirectDependentElementId(newNonDirectConnectedIds);
                UpdateSupportingElmnts(supElmntCon.ConnectedWithId, newNonDirectConnectedIds);
            }
        }
        //static void IncreaseDirectConnectionsNumber(int elementId, int number)
        //    => _cahsedGraphElements[elementId].TotDirctCons += number;
        //static void IncreaseDirectSupporttedElements(int elementId, int number)
        //{
        //    _cahsedGraphElements[elementId].TotDirctSuportedElmns += number;
        //    IncreaseTotalSupportingElements(elementId, number);

        //}
        //static void IncreaseTotalSupportingElements(int elementId, int number)
        //    => _cahsedGraphElements[elementId].TotSuportedElmns += number;
        static void GenerateGraph(List<Revit.Elements.Element> allBuildingElements, List<List<int>> connectedIds)
        {

            var nodes = GetNodes(allBuildingElements);
            var links = "";
            for (int i = 0; i < allBuildingElements.Count; i++)
            {
                links += GetLinks(allBuildingElements[i], connectedIds[i]);
                if (i != allBuildingElements.Count - 1 && connectedIds[i].Count > 0)
                    links += ",";
            }
            GenerateGraph(nodes, links);

        }
        static void GenerateGraph(Revit.Elements.Element baseElement, List<Revit.Elements.Element> connectedElements)
        {
            var nodes = GetNodes(new List<Revit.Elements.Element>(connectedElements) { baseElement });
            var links = GetLinks(baseElement, connectedElements);
            GenerateGraph(nodes, links);

        }
        static List<GraphElementView> GenerateGraph(List<GraphElement> elements)
        {
            var nodes = GetNodes(elements);
            var links = GetLinks(elements);
            var jsSerilaizer = new JavaScriptSerializer();
            var elementsView = elements.Select(e => new GraphElementView() { Id = e.Id, Category = e.Category, IsStructural = e.IsStructural, Material = e.Material, Volume = e.Volume, AllDependentElementsIds = e.GetAllDependentElementsIds().ToList(), DirectConnectedElementsIds = e.GetDirectConnectedElementsIds().ToList(), DirectSupportedElementsIds = e.GetDirectSupportedElementsIds().ToList(), NonDirectDependentElementsIds = e.GetNonDirectDependentElementsIds().ToList(), NonSupportingDirectElementsIds = e.GetNonSupportingDirectElementsIds().ToList(), DirectConnectedStructuralElementsIds = e.GetDirectConnectedStructuralElementsIds().ToList() }).ToList();
            GenerateGraph(nodes, links, jsSerilaizer.Serialize(elementsView));
            return elementsView;


        }
        static int GetConnectionsNumberForColoring(GraphElement element)
        {
            switch (_graphConfig.ColoringMethod)
            {
                case GraphColoringMethod.AllDependentElements:
                    return element.GetAllDependentElementsIds().Count();
                case GraphColoringMethod.DirectSupportedElements:
                    return element.GetDirectSupportedElementsIds().Count();
                case GraphColoringMethod.NonDirectDependentElements:
                    return element.GetNonDirectDependentElementsIds().Count();
                case GraphColoringMethod.DirectConnectedElements:
                    return element.GetDirectConnectedElementsIds().Count();
                case GraphColoringMethod.DirectConnectedStructuralElements:
                    return element.GetDirectConnectedStructuralElementsIds().Count();
                case GraphColoringMethod.NonSupportingDirectElements:
                    return element.GetNonSupportingDirectElementsIds().Count();
                default:
                    return -1;
            }
        }
        static string GetNodes(List<GraphElement> elements)
        {
            var ret = new StringBuilder();
            for (int i = 0; i < elements.Count; i++)
            {
                ret.AppendLine($" {{ key: {elements[i].Id}, text: '{GetPreparedName(elements[i])}' , fill: '{GetEquivalendHtmlColorCode(GetGraphNodeColor(elements[i]))}' }}");
                //  ret.AppendLine($" {{ key: {elements[i].Id}, text: '{GetPreparedName(elements[i])}'  }}");

                if (i != elements.Count - 1)
                    ret.Append(",");
            }
            return ret.ToString();
        }

        private static string GetGraphNodeColor(GraphElement graphElement)
        {
            var dependetConnections = GetConnectionsNumberForColoring(graphElement);
            //var totDepElements = graphElement.GetAllDependentElementsIds().Count();
            if (dependetConnections <= _graphConfig.PerfectDepdNum)
                return "Green"; //"#008000";
            if (dependetConnections <= _graphConfig.GoodDepNum)
                return "Yellow";// "#FFFF00";
            return "Red";// "#FF0000";

        } //#FF0000 red / #DC143C crimson , #008000 green ,#FFFF00 yellow
        static string GetEquivalendHtmlColorCode(string colorName)
        {
            if (colorName.Equals("Green"))
                return "#008000";
            else if (colorName.Equals("Yellow"))
                return "#FFFF00";
            else if (colorName.Equals("Red"))
                return "#FF0000";
            return "UnKnownColor";
        }
        static DSCore.Color GetEquivalenRevitColor(string colorName)
        {
            //var redColor = DSCore.Color.ByARGB(255, 255, 0, 0);
            //var greenColor = DSCore.Color.ByARGB(255, 0, 255, 0);
            //var yellowColor = DSCore.Color.ByARGB(255, 255, 255, 0);
            if (colorName.Equals("Green"))
                return DSCore.Color.ByARGB(255, 0, 255, 0);
            else if (colorName.Equals("Yellow"))
                return DSCore.Color.ByARGB(255, 255, 255, 0);
            else if (colorName.Equals("Red"))
                return DSCore.Color.ByARGB(255, 255, 0, 0);
            return null;
        }
        static string GetLinks(List<GraphElement> elements)
        {

            var ret = new StringBuilder();
            for (int n = 0; n < elements.Count; n++)
            {
                var element = elements[n];
                var elmntConnections = element.GetConnections().Where(c => c.ConnectionType != ConnectionType.SupportedBy).ToList();
                for (int i = 0; i < elmntConnections.Count; i++)
                {
                    ret.AppendLine($" {{ from: {element.Id}, to: {elmntConnections[i].ConnectedWithId}, text: '{GetConnectionTypeString(elmntConnections[i].ConnectionType)}' }}");
                    if (!(n == elements.Count - 1 && i == elmntConnections.Count - 1))
                        ret.Append(",");
                }
            }
            return ret.ToString();
        }
        static string GetConnectionTypeString(ConnectionType conType)
        {
            switch (conType)
            {
                case ConnectionType.Supporting:
                    return "S";
                case ConnectionType.SupportedBy:
                    return "SB";

                case ConnectionType.Beside:
                    return "B";

                case ConnectionType.NotKnown:
                    return "U";

                default:
                    return "";

            }
        }
        static void GenerateGraph(string nodesText, string linksText, string nodesData)
        {
            var dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            var templatePath = new Uri(Path.Combine(dllPath, "TemplateGraph.html")).LocalPath;// @"F:\Reversible Building\TemplateGraph.html";
            var generatedFilePath = new Uri(Path.Combine(dllPath, "GeneratedGraph.html")).LocalPath; // @"F:\Reversible Building\GeneratedGraph.html";
            var generatedGraph = File.ReadAllText(templatePath);
            generatedGraph = generatedGraph.Replace("@GeneratedNodes", nodesText).Replace("@GeneratedLinks", linksText).Replace("#graphElmnts", nodesData);
            File.WriteAllText(generatedFilePath, generatedGraph);
            System.Diagnostics.Process.Start(generatedFilePath);
        }
        static void GenerateGraph(string nodesText, string linksText)
        {

            var dllPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            var templatePath = new Uri(Path.Combine(dllPath, "TemplateGraph.html")).LocalPath;// @"F:\Reversible Building\TemplateGraph.html";
            var generatedFilePath = new Uri(Path.Combine(dllPath, "GeneratedGraph.html")).LocalPath; // @"F:\Reversible Building\GeneratedGraph.html";
            var generatedGraph = File.ReadAllText(templatePath);

            generatedGraph = generatedGraph.Replace("@GeneratedNodes", nodesText).Replace("@GeneratedLinks", linksText);
            File.WriteAllText(generatedFilePath, generatedGraph);
            System.Diagnostics.Process.Start(generatedFilePath);
        }
        static BoundingBox GetElementBoundaryBox(Revit.Elements.Element element)
        => _cahsedGraphElements.Count > 0 ? _cahsedGraphElements[element.Id].BoundingBox : element.BoundingBox;
        static IEnumerable<Geometry> GetElementGeometries(Revit.Elements.Element element)
        => _cahsedGraphElements.Count > 0 ? _cahsedGraphElements[element.Id].Geometries : element.Geometry().Cast<Geometry>();


        //static List<Connection> GetConnections(Revit.Elements.Element element, List<Revit.Elements.Element> otherElements)
        //=> GetIntersectedElements(element, otherElements).Select(i => new Connection() { ConnectedWithId = i.Id }).ToList();

        static bool ContainsAny(this string s, params string[] list)
            => list.FirstOrDefault(e => s.Contains(e)) != null;
        static Connection GetConnection(this Revit.Elements.Element element, Revit.Elements.Element otherElement)
        {
            var conType = GetConnectionType(element, otherElement);
            //the data structure of checking elements checks structure elements firsr, so if a structural element is supported by other structural element, we have to ignore that kind of relationship between that structal element and other nonstructual elements(load distribution)
            //check the intersection between floors and above columns to determine the type of connection based on user configuration
            //usually if a column is above a floor and they intersect and the the column is supporteted by other strElement, we cant say here the floor supports the column but the user can change that behavior (something related to reversible buildings and the type of connections between elements)
            if ((otherElement.GetCategory.Name.Contains(floorCategoryFilter) && !_graphConfig.FloorSupportsAnyAboveColumn))
                if (element.GetCategory.Name.Contains(columnCategoryFilter) || (element.GetCategory.Name.Contains(wallCategoryFilter) && element.IsStructural()))
                    if (conType == ConnectionType.SupportedBy)
                        if (_cahsedGraphElements[element.Id].GetConnections().FirstOrDefault(c => c.ConnectionType == ConnectionType.SupportedBy) != null)
                            conType = ConnectionType.NotKnown;

            return new Connection() { ConnectedWithId = otherElement.Id, ConnectionType = conType, IsConnectedWithStructuralElement = otherElement.IsStructural() };


        }
        static ConnectionType GetConnectionType(this Revit.Elements.Element element, Revit.Elements.Element otherElement)
        {

            if (element.IsAbove(otherElement) || element.IsContainedBy(otherElement))
                return ConnectionType.SupportedBy;
            if (element.IsBelow(otherElement) || element.ContainsGeomtetricOf(otherElement))
                return ConnectionType.Supporting;
            return ConnectionType.Beside;


        }
        static bool IsBeside(this Revit.Elements.Element element, Revit.Elements.Element otherElement)
        {
            var elmntBoundBox = GetElementBoundaryBox(element);
            var otherElmnBoundBox = GetElementBoundaryBox(otherElement);
            return Math.Abs(otherElmnBoundBox.MaxPoint.Z - elmntBoundBox.MaxPoint.Z) < .0001 && Math.Abs(otherElmnBoundBox.MinPoint.Z - elmntBoundBox.MinPoint.Z) < .0001;
        }
        static bool IsAbove(this Revit.Elements.Element element, Revit.Elements.Element otherElement)
        {
            var elmntBoundBox = GetElementBoundaryBox(element);
            var otherElmnBoundBox = GetElementBoundaryBox(otherElement);
            return elmntBoundBox.MaxPoint.Z >= otherElmnBoundBox.MaxPoint.Z && elmntBoundBox.MinPoint.Z >= otherElmnBoundBox.MinPoint.Z;
        }
        static bool IsContainedBy(this Revit.Elements.Element element, Revit.Elements.Element otherElement)
        {
            var elmntBoundBox = GetElementBoundaryBox(element);
            var otherElmnBoundBox = GetElementBoundaryBox(otherElement);
            return elmntBoundBox.MaxPoint.Z <= otherElmnBoundBox.MaxPoint.Z && elmntBoundBox.MinPoint.Z >= otherElmnBoundBox.MinPoint.Z;
        }
        //static bool IsCoreElement(this Revit.Elements.Element element) 
        //    => element.GetCategory.Name.ContainsAny(columnCategoryFilter,beamsCategoryFilter,foundationCategoryFilter) ||( element.IsStructural());
        static bool IsStructural(this Revit.Elements.Element element)
            => element.GetCategory.Name.ContainsAny(foundationCategoryFilter, columnCategoryFilter, beamsCategoryFilter) || (element.GetParameterValueByName("Structural")?.ToString().Equals("1") ?? false);
        static bool ContainsGeomtetricOf(this Revit.Elements.Element element, Revit.Elements.Element otherElement)
            => otherElement.IsContainedBy(element);

        static bool IsBelow(this Revit.Elements.Element element, Revit.Elements.Element otherElement)
       => otherElement.IsAbove(element);

        static IEnumerable<Revit.Elements.Element> GetIntersectedElements(Revit.Elements.Element element, List<Revit.Elements.Element> otherElements)
        {
            var elmntBoundBox = GetElementBoundaryBox(element);
            var elmntGeometries = GetElementGeometries(element);
            foreach (var otherElement in otherElements)
                if (elmntBoundBox.Intersects(GetElementBoundaryBox(otherElement)))
                    if (elmntGeometries.Intersect(GetElementGeometries(otherElement)))
                        yield return otherElement;
        }
        static bool Intersect(this IEnumerable<Geometry> obj1Geoms, IEnumerable<Geometry> obj2Geoms)
        {
            foreach (var geomObj1 in obj1Geoms)
                foreach (var geomObj2 in obj2Geoms)
                    if (geomObj1.DoesIntersect(geomObj2))
                        return true;
            return false;
        }
        static string GetPreparedName(Revit.Elements.Element element)
        {
            //   var parameters = element.Parameters;
            return $"{GetParameterValueByName(element, "Category")}-{element.Id}"; // ({GetParameterValueByName(parameters, "Family and Type")}) ";
        }
        static string GetPreparedName(GraphElement element)
        {
            return $"{element.Category.Replace("Structural", "")}-{element.Id} [{GetConnectionsNumberForColoring(element)}]"; // ({GetParameterValueByName(parameters, "Family and Type")}) ";
        }

        static string GetParameterValueByName(Revit.Elements.Element element, string name)
        {

            var parameters = element.Parameters;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].Name.Equals(name))
                    return parameters[i].Value.ToString();
            return null;

        }

        static string GetNodes(List<Revit.Elements.Element> elements)
        {
            var ret = new StringBuilder();
            for (int i = 0; i < elements.Count; i++)
            {
                ret.AppendLine($" {{ key: {elements[i].Id}, text: '{GetPreparedName(elements[i])}' }}");
                if (i != elements.Count - 1)
                    ret.Append(",");
            }
            return ret.ToString();
        }

        static string GetLinks(Revit.Elements.Element baseElement, List<Revit.Elements.Element> connectedElements)
        => GetLinks(baseElement, connectedElements.Select(c => c.Id).ToList());


        static string GetLinks(Revit.Elements.Element baseElement, List<int> connectedIds)
        {

            var ret = new StringBuilder();

            for (int i = 0; i < connectedIds.Count; i++)
            {
                ret.AppendLine($" {{ from: {baseElement.Id}, to: {connectedIds[i]}, text: '' }}");
                if (i != connectedIds.Count - 1)
                    ret.Append(",");
            }

            return ret.ToString();
        }
    }


}
