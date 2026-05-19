namespace MakeDoc.Core.Models
{
    public class NodeHierarchy
    {
        public string ParentNodeID { get; set; } = string.Empty;
        public string ChildNodeID { get; set; } = string.Empty;
        public string DocTypeID { get; set; } = string.Empty;
        public int Sequence { get; set; } = 0;
    }
}