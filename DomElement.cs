﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jtc.ExtensionMethods;
using System.Diagnostics;

namespace Jtc.CsQuery
{
    [Flags]
    public enum DomRenderingOptions
    {
        RemoveMismatchedCloseTags = 1,
        RemoveComments = 2
    }
    public enum DocType
    {
        HTML5 = 1,
        HTML4 = 2,
        XHTML = 3,
        Unknown = 4
    }
    public enum NodeType
    {
        ELEMENT_NODE  =1,
        //ATTRIBUTE_NODE =2,
        TEXT_NODE = 3,
        CDATA_SECTION_NODE = 4,
        //ENTITY_REFERENCE_NODE = 5,
        //ENTITY_NODE=  6,
        //PROCESSING_INSTRUCTION_NODE =7,
        COMMENT_NODE =  8,
        DOCUMENT_NODE =  9,
        DOCUMENT_TYPE_NODE = 10,
        //DOCUMENT_FRAGMENT_NODE = 11,
        //NOTATION_NODE  =12
    }
    public interface IDomObject
    {
        IDomContainer Parent { get; set; }
        NodeType NodeType {get;}
        string PathID {get;}
        string Path { get; }
        DomRoot Root { get; set;}
        string Html { get; }
        void AddToIndex();
        void RemoveFromIndex();
        IDomObject Clone();
        bool InnerHtmlAllowed {get;}
        bool Complete { get; }        
    }
    /// <summary>
    /// Defines an interface for elements whose defintion (not innerhtml) contain non-tag or attribute formed data
    /// </summary>
    public interface IDomSpecialElement: IDomObject 
    {
        string NonAttributeData { get; set; }
        string Text { get; set; }
    }
    public interface IDomText : IDomObject
    {
    }
    /// <summary>
    /// A marker interface an element that will be rendered as text because it was determined to be a mismatched tag
    /// </summary>
    public interface IDomInvalidElement : IDomText
    {
        
    }
    public interface IDomComment :  IDomSpecialElement
    {
        bool IsQuoted { get; set; }
    }
    public interface IDomCData :  IDomSpecialElement
    {
    }
    public interface IDomDocumentType :  IDomSpecialElement
    {
    }
    public interface IDomContainer : IDomObject
    {
        IEnumerable<IDomObject> Children {get;}
        IEnumerable<IDomElement> Elements { get; }
        void Add(IDomObject element);
        void AddRange(IEnumerable<IDomObject> element);
        void Remove(IDomObject element);
        void RemoveChildren();
        void Insert(IDomObject element, int index);
        string GetNextChildID();
        int Count { get; }
    }
    public interface IDomRoot : IDomContainer
    {
        DocType DocType { get; set; }
        DomRenderingOptions DomRenderingOptions { get; set; }
    }
    public interface IDomElement : IDomContainer
    {
        IEnumerable<string> Classes { get; }
        IEnumerable<KeyValuePair<string, string>> Styles { get; }

        bool HasClass(string className);
        bool AddClass(string className);
        bool RemoveClass(string className);
        void AddStyle(string name, string value);
        void AddStyle(string styleString);
        bool RemoveStyle(string name);
        string GetStyle(string name);

        void SetAttribute(string name);
        void SetAttribute(string name, string value);
        string GetAttribute(string name);
        string GetAttribute(string name, string defaultValue);
        bool TryGetAttribute(string name, out string value);
        bool HasAttribute(string name);
        bool RemoveAttribute(string name);

        string Tag { get; set; }
        string ID { get; set; }
        string Style { get; }
        string Class { get; }
        string InnerHtml { get; }
        IEnumerable<KeyValuePair<string, string>> Attributes { get; }
        string this[string index] { get; set; }

        string ElementHtml { get; }
    }
    /// <summary>
    /// Base class for anything that exists in the DOM
    /// </summary>
    /// 
    public abstract class DomObject<T>: IDomObject where T: IDomObject,new()
    {
        public abstract bool InnerHtmlAllowed { get;}
        public virtual DomRoot Root { get; set; }
        public abstract NodeType NodeType { get; }
        public virtual T Clone()
        {
            T clone = new T();
            clone.Root = Root;
            clone.Parent = null;
            // prob should just implemnt this in the subclass but easier for now
            if (clone is IDomSpecialElement)
            {
                ((IDomSpecialElement)clone).NonAttributeData = ((IDomSpecialElement)this).NonAttributeData;
            }
            return clone;
        }

        // Unique ID assigned when added to a dom
        public string PathID
        {
            get
            {
                if (_PathID ==null) {

                    _PathID = (Parent == null ? String.Empty : Parent.GetNextChildID());
               }
               return _PathID;
            }

        } protected string _PathID = null;
        public string Path {
            get
            {
                if (_Path != null) {
                    return _Path;
                }
                return (Parent == null ? String.Empty : Parent.Path + "/") + PathID;
            }
        }
        protected string _Path = null;
        
        public IDomContainer Parent
        {
            get
            {
                return _Parent;
            }
            set
            {
                _Path = null;
                _PathID = null;
                _Parent = value;
            }
        }

        protected IDomContainer _Parent = null;
        public abstract bool Complete { get; }
        public abstract string Html
        { get;  }
        protected int IDCount = 0;

        protected IEnumerable<string> IndexKeys()
        {
            if (!(this is DomElement)) {
                yield break;
            }

            DomElement e = this as DomElement;
            if (!Complete)
            {
                throw new Exception("This element is incomplete and cannot be added to a DOM.");
            }
            // Add just the element to the index no matter what so we have an ordered representation of the dom traversal
            yield return IndexKey(String.Empty);
            yield return IndexKey(e.Tag);
            if (!String.IsNullOrEmpty(e.ID))
            {
                yield return IndexKey("#" + e.ID);
            }
            foreach (string cls in e.Classes)
            {
                yield return IndexKey("." + cls);
            }
            //todo -add attributes?
        }
        protected int UniqueID = 0;

        public void AddToIndex()
        {
            if (Root!=null && this is IDomElement)
            {
                // Fix the path when it's added to the index.
                // This is a little confusing. Would rather that we can't access it until it's added to a DOM.
                _Path = Path;
                foreach (string key in IndexKeys())
                {
                    AddToIndex(key);
                }
                if (this is IDomContainer)
                {
                    IDomContainer e = (IDomContainer)this;

                    foreach (IDomObject child in e.Children)
                    {
                        // Move root in case this is coming from an unmapped or alternate DOM
                        child.Root = Root;
                        child.AddToIndex();
                    }
                }
            }
        }
        public void RemoveFromIndex()
        {
            if (Root!=null && this is IDomElement)
            {
                if (this is IDomContainer)
                {
                    IDomContainer e = (IDomContainer)this;

                    foreach (DomElement child in e.Elements)
                    {
                        child.RemoveFromIndex();
                    }
                }
                foreach (string key in IndexKeys())
                {
                    RemoveFromIndex(key);
                }
            }
        }
        /// <summary>
        /// Remove only a single index, not the entire object
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="key"></param>
        public void RemoveFromIndex(string key)
        {
            Root.SelectorXref.Remove(key);
        }
        public void AddToIndex(string key)
        {
            Root.SelectorXref.Add(key, this as DomElement);
        }
        protected string IndexKey( string key)
        {
            return key + ">" + Path;
        }

        IDomObject IDomObject.Clone()
        {
            return Clone();
        }

        
    }
    
    

    /// <summary>
    /// Catch-all for unimplemented node types (e.g.
    /// </summary>
    
    public class DomDocumentType : DomObject<DomDocumentType>,IDomDocumentType 
    {
        public DomDocumentType()
            : base()
        {

        }
        public override NodeType NodeType
        {
            get { return NodeType.DOCUMENT_TYPE_NODE; }
        }
        public DocType DocType
        {
            get
            {
                if (_DocType != 0)
                {
                    return _DocType;
                }
                else
                {
                    return GetDocType();
                }
            }
            set
            {
                _DocType = value;
            }
        }
        protected DocType _DocType = 0;

        public override string  Html
        {
            get { return "<!DOCTYPE " + NonAttributeData + ">"; }
        }

        public string  NonAttributeData
        {
            get
            {
                if (_DocType == 0)
                {
                    return _NonAttributeData;
                }
                else
                {
                    switch (_DocType)
                    {
                        case DocType.HTML5:
                            return "html";
                        case DocType.XHTML:
                            return "html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\"";
                        case DocType.HTML4:
                            return "html PUBLIC \"-//W3C//DTD HTML 4.01 Frameset//EN\" \"http://www.w3.org/TR/html4/frameset.dtd\"";
                        default:
                            throw new Exception("Unimplemented doctype");
                    }

                }
            }
	        set 
	        { 
		        _NonAttributeData = value;
	        }
        }
        protected string _NonAttributeData = String.Empty;
        protected DocType GetDocType()
        {
            string data = NonAttributeData.Trim().ToLower();
            if (data == "html")
            {
                return DocType.HTML5;
            } else if (data.IndexOf("xhtml 1")>=0) {
                return DocType.XHTML;
            }
            else if (data.IndexOf("html 4") >= 0)
            {
                return DocType.HTML4;
            }
            else
            {
                return DocType.Unknown;
            }
        }

        public override bool Complete
        {
            get { return true; }
        }
        public override bool InnerHtmlAllowed
        {
            get { return false; }
        }
        #region IDomSpecialElement Members

        public string Text
        {
            get
            {
                return NonAttributeData;
            }
            set
            {
                NonAttributeData = value;
            }
        }

        #endregion

        
  
    }
    public class DomCData : DomObject<DomCData>, IDomCData
    {
        public DomCData()
            : base()
        {

        }
        public override NodeType NodeType
        {
            get { return NodeType.CDATA_SECTION_NODE; }
        }
        public override string Html
        {
            get { return "<![CDATA[" + NonAttributeData + ">"; }
        }

        #region IDomSpecialElement Members

        public string NonAttributeData
        {
            get;
            set;
        }
        public override bool InnerHtmlAllowed
        {
            get { return false; }
        }
        public override bool Complete
        {
            get { return true; }
        }
        public string Text
        {
            get
            {
                return NonAttributeData;
            }
            set
            {
               NonAttributeData=value;
            }
        }

        #endregion
    }
    public class DomComment : DomObject<DomComment>, IDomComment 
    {
        public DomComment()
            : base()
        {
        }
        public override NodeType NodeType
        {
            get { return NodeType.COMMENT_NODE; }
        }
        public bool IsQuoted { get; set; }
        protected string TagOpener
        {
            get { return IsQuoted ? "<!--" : "<!"; }
        }
        protected string TagCloser
        {
            get { return IsQuoted ? "-->" : ">"; }
        }
        public override string Html
        {
            get {
                if (Root.DomRenderingOptions.HasFlag(DomRenderingOptions.RemoveComments))
                {
                    return String.Empty;
                }
                else
                {
                    return TagOpener + NonAttributeData + TagCloser;
                }
            }
        }

        public override bool InnerHtmlAllowed
        {
            get { return false; }
        }
        public override bool Complete
        {
            get { return true; }
        }
        #region IDomSpecialElement Members

        public string NonAttributeData
        {
            get;
            set;
        }

        public string Text
        {
            get
            {
                return NonAttributeData;
            }
            set
            {
                NonAttributeData = value;
            }
        }

        #endregion
    }
    /// <summary>
    /// Used for literal text (not part of a tag)
    /// </summary>
    public class DomText : DomObject<DomText>, IDomText
    {
        public DomText()
        {
            Initialize();
        }
        public DomText(string text): base()
        {
            Initialize();
            Text = text;
        }
        protected void Initialize()
        {
            Text = String.Empty;
        }
        public override NodeType NodeType
        {
            get { return NodeType.TEXT_NODE; }
        }
        public string Text
        {
            get;
            set;
        }

        public override string Html
        {
            get
            {
                return Text;
            }
        }
        public override DomText Clone()
        {
            DomText domText = base.Clone();
            domText.Text = Text;
            return domText;
        }
        
        public override bool InnerHtmlAllowed
        {
            get { return false; }
        }
        public override bool Complete
        {
            get { return !String.IsNullOrEmpty(Text);  }
        }
    }

    public class DomInvalidElement : DomText, IDomInvalidElement
    {
        public DomInvalidElement()
            : base()
        {
        }
        public DomInvalidElement(string text): base(text)
        {

        }
        public override string Html
        {
            get
            {
                if (Root.DomRenderingOptions.HasFlag(DomRenderingOptions.RemoveMismatchedCloseTags)) {
                    return String.Empty;
                } else {
                    return base.Html;
                }
            }
        }
    }
    /// <summary>
    /// Base class for Dom object that contain other elements
    /// </summary>
    public abstract class DomContainer<T> : DomObject<T>, IDomContainer where T: IDomObject,IDomContainer, new()
    { 
        public DomContainer()
        {

        }
        
        public DomContainer(IEnumerable<IDomObject> elements)
        {
            _Children.AddRange(elements);   
        }

        /// <summary>
        /// Returns all children (including inner HTML as objects);
        /// </summary>
        public IEnumerable<IDomObject> Children
        {
            get
            {
                return _Children;
            }
        }
        internal List<IDomObject> _Children
        {
            get
            {
                if (__Children == null)
                {
                    __Children = new List<IDomObject>();
                }
                return __Children;
            }
        } 
        protected List<IDomObject> __Children = null;
        /// <summary>
        /// Returns all elements
        /// </summary>
        public IEnumerable<IDomElement> Elements
        {
            get
            {
                foreach (IDomObject elm in Children)
                {
                    if (elm is DomElement)
                    {
                        yield return (IDomElement)elm;
                    }
                }
                yield break;
            }
        }
        public IDomObject this[int index]
        {
            get
            {
                return _Children[index];
            }
        }
        public int Count
        {
            get
            {
                return _Children.Count;
            }
        }
        public override string Html
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (IDomObject e in Children )
                {
                    sb.Append(e.Html);
                }
                return (sb.ToString());
            }
        } 
       /// <summary>
        /// Add a child to this element 
        /// </summary>
        /// <param name="element"></param>
        public virtual void Add(IDomObject element)
        {
            if (!this.InnerHtmlAllowed)
            {
                throw new Exception("Cannot add children to this element type. Inner HTML is not allowed.");
            }
            element.Parent = this;
            element.Root = this.Root;
            //AddPath(element);
            _Children.Add(element);
            element.AddToIndex();

        }

        /// </summary>
        /// <param name="elements"></param>
        public virtual void AddRange(IEnumerable<IDomObject> elements)
        {
            foreach (IDomObject e in elements)
            {
                Add(e);
            }
        }
        /// <summary>
        /// Adds a child element at a specific index
        /// </summary>
        /// <param name="index"></param>
        /// <param name="element"></param>
        public void Insert(IDomObject element,int index)
        {
            element.Parent = this;
            element.Root = this.Root;
            //AddPath(element);
            _Children.Insert(index, element);
            element.AddToIndex();
        }
   
        public void Remove(IDomObject element)
        {

            _Children.Remove(element);
            element.RemoveFromIndex();
           
            element.Parent = null;
            element.Root = null;
        }
 
        /// <summary>
        /// Removes all children
        /// </summary>
        public void RemoveChildren()
        {
            for (int i=_Children.Count-1;i>=0;i--)
            {
                Remove(_Children[i]);
            }
        }

        public override T Clone()
        {

            T clone = base.Clone();
            foreach (IDomObject obj in _Children) {
                clone.Add(obj.Clone());
           }
            return clone;
        }
        
        /// <summary>
        /// This is used to assign sequential IDs to children. Since they are requested by the children the method needs to be maintained in the parent.
        /// </summary>
        public string GetNextChildID()
        {
            
            return Base62Code(++IDCount);
            
        }
        // Just didn't use the / and the +. A three character ID will permit over 250,000 possible children at each level
        // so that should be plenty
        protected string Base62Code(int number)
        {
            string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz=";
            string output = String.Empty;
            int cur = 0;
            do
            {

                if (number >= 62)
                {
                    cur = (int)Math.Floor((float)(number / 62));
                    number /= cur * 62;
                }
                else
                {
                    cur = number;
                    number = -1;
                }
                output += chars[cur];
            } while (number >= 0);
            return output.PadLeft(3, '0');
        }
    }

    /// <summary>
    /// Special node type to represent the DOM.
    /// </summary>
    public class DomRoot : DomContainer<DomRoot>,IDomRoot 
    {
        public DomRoot()
            : base()
        {
        }
        public DomRoot(IEnumerable<IDomObject> elements)
            : base(elements)
        {

        }
        public DomRenderingOptions DomRenderingOptions
        { 
            get
            {
             return _DomRenderingOptions;
            } 
            set
            {
                _DomRenderingOptions=value;
            } 
        }

        protected DomRenderingOptions _DomRenderingOptions = DomRenderingOptions.RemoveMismatchedCloseTags;
        public override DomRoot  Root
        {
	          get 
	        { 
		         return this;
	        }
	          set 
	        { 
		        throw new Exception("You cannot set the Root for a DomRoot type object.");
	        }
        }
        public override NodeType NodeType
        {
            get { return NodeType.DOCUMENT_NODE; }
        }
        public DomDocumentType  DocTypeNode {
            get
            {
                foreach (IDomObject obj in Root.Children)
                {
                    if (obj.NodeType == NodeType.DOCUMENT_TYPE_NODE)
                    {
                        return (DomDocumentType)obj;
                    }
                }
                return null;
            }
        }
        public DocType DocType
        {
            get
            {
                if (_DocType==0) {
                    DomDocumentType docType = DocTypeNode;
                    if (docType == null)
                    {
                        _DocType = DocType.XHTML;
                    }
                    else
                    {
                        _DocType = docType.DocType;
                    }
                }
                return _DocType;
            }
            set
            {
                _DocType = value;
            }
        }
        protected DocType _DocType = 0;
        public RangeSortedDictionary<DomElement> SelectorXref = new RangeSortedDictionary<DomElement>();
        public override bool InnerHtmlAllowed
        {
            get { return true; }
        }
        public override bool Complete
        {
            get { return true; }
        }
    }
    
    /// <summary>
    /// HTML elements
    /// </summary>
    public class DomElement : DomContainer<DomElement>, IDomElement
    {
        public DomElement()
        {
        }
        public override NodeType NodeType
        {
            get { return NodeType.ELEMENT_NODE; }
        }
        protected Dictionary<string, string> _Attributes = new Dictionary<string, string>();
        /// <summary>
        /// Creates a deep clone of this
        /// </summary>
        /// <returns></returns>
        public override DomElement Clone()
        {
            DomElement e = new DomElement();
            e.Tag = Tag;
            
            e._Styles = new Dictionary<string, string>(_Styles);
            e._Classes = new HashSet<string>(_Classes);
            foreach (var attr in _Attributes)
            {
                e.SetAttribute(attr.Key, attr.Value);
            }
            foreach (IDomObject obj in _Children)
            {
                if (obj is DomElement)
                {
                    e.Add(((DomElement)obj).Clone());
                }
                else if (obj is IDomText)
                {
                    DomText lit = new DomText(((IDomText)obj).Html);
                    e.Add(lit);
                } else {
                    throw new Exception("Unexpected element type while cloning a DomElement");
                }
            }
            return e;
        }

        public IEnumerable<string> Classes
        {
            get
            {
                foreach (string val in _Classes)
                {
                    yield return val;
                }
            }
        } protected HashSet<string> _Classes = new HashSet<string>();
        public IEnumerable<KeyValuePair<string,string>> Styles
        {
            get
            {
                foreach (var kvp in _Styles)
                {
                    yield return kvp;
                }
            }
        } protected Dictionary<string, string> _Styles = new Dictionary<string, string>();

        public bool HasClass(string name)
        {
            return _Classes.Contains(name);
        }
        public bool AddClass(string name)
        {
            if (_Classes.Add(name))
            {
                AddToIndex(IndexKey("."+name));
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool RemoveClass(string name)
        {
            if (_Classes.Remove(name))
            {
                RemoveFromIndex(IndexKey("." + name));
            }
            return false;
        }
        public void AddStyle(string name, string value)
        {
            _Styles[name.Trim()] = value.Replace(";",String.Empty).Trim();
        }
        public void AddStyle(string style)
        {
            string[] kvp = style.Split(':');
            AddStyle(kvp[0], kvp[1]);
        }
        public bool RemoveStyle(string name)
        {
            return _Styles.Remove(name);
        }
        public string GetStyle(string name)
        {
            string value;
            if (_Styles.TryGetValue(name,out value)) {
                return value;
            }
            return null;
        }
        public bool HasAttribute(string name)
        {
            string value;
            if (_Attributes.TryGetValue(name.ToLower(), out value))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public void SetAttribute(string name, string value)
        {
            string lowName = name.ToLower();
            // TODO this is not right, should be able to set Class attribute, seaprate this handling
            switch (lowName)
            {
                case "class":
                    _Classes.Clear();
                    foreach (string val in value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        _Classes.Add(val);
                    }
                    break;
                case "style":
                    _Styles.Clear();
                    string[] styles = value.Trim().Split(new char[] {';'},StringSplitOptions.RemoveEmptyEntries);
                    foreach (string val in styles)
                    {
                        if (val.IndexOf(":") > 0)
                        {
                            string[] kvps = val.Split(':');
                            _Styles[kvps[0]] = kvps[1];
                        }
                    }
                    break;
                default:
                    _Attributes[lowName] = value;
                    break;
            }
        }
        /// <summary>
        /// Sets an attribute with no value
        /// </summary>
        /// <param name="name"></param>
        public void SetAttribute(string name)
        {
            SetAttribute(name, String.Empty);
        }
        public bool RemoveAttribute(string name)
        {
            return _Attributes.Remove(name);
        }

        /// <summary>
        /// Gets an attribute value, or returns null if the value is missing. If a valueless attribute is found, this will also return null. HasAttribute should be used
        /// to test for such attributes. Attributes with an empty string value will return String.Empty.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetAttribute(string name)
        {
            return GetAttribute(name, null);
        }
        /// <summary>
        /// Returns the value of an attribute or a default value if it could not be found.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetAttribute(string name, string defaultValue)
        {
            string value;
            switch (name.ToLower())
            {
                case "style":
                    return Style;
                case "class":
                    return Class;
            }
            if (_Attributes.TryGetValue(name.ToLower(), out value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }
        public bool TryGetAttribute(string name, out string value)
        {
            return _Attributes.TryGetValue(name.ToLower(),out  value);

        }
        
        public string Style
        {
            get
            {
                string style = String.Empty;
                foreach (var kvp in _Styles)
                {
                    style += (style==String.Empty?String.Empty:" ") + kvp.Key + ": " + kvp.Value + ";";
                }
                return style;
            }
        }
        public string Class
        {
            get
            {
                string cls = String.Empty;
                foreach (var val in _Classes)
                {
                    cls += (cls == String.Empty ? String.Empty : " ") + val;
                }
                return cls;
            }
        }
        public IEnumerable<KeyValuePair<string, string>> Attributes
        {
            get
            {
                foreach (string key in _Attributes.Keys)
                {
                    yield return new KeyValuePair<string, string>(key.ToLower(), _Attributes[key]);
                }
            }
        }
        public string Tag
        {
            get
            {
                return _Tag;
            }
            set
            {
                if (String.IsNullOrEmpty(Tag))
                {
                    _Tag = value.ToLower();
                }
                else
                {
                    throw new Exception("You can't change the tag of an element once it has been created.");
                }
                
            }
        } protected string _Tag = null;
        public string ID
        {
            get
            {
                return GetAttribute("id",String.Empty);
            }
            set
            {
                string id = _Attributes["id"];
                if (!String.IsNullOrEmpty(id))
                {
                    RemoveFromIndex(IndexKey("#" + id));
                }
                _Attributes["id"] = value;
                AddToIndex(IndexKey("#" + value));
            }
        }

        public string this[string name]
        {
            get
            {
                return GetAttribute(name);
            }
            set
            {
                SetAttribute(name, value);
            }
        }
        /// <summary>
        /// Returns text of the inner HTMl
        /// </summary>
        public string InnerHtml
        {
            get
            {
                if (Children.IsNullOrEmpty())
                {
                    return String.Empty;
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (IDomObject elm in Children)
                    {
                        sb.Append(elm.Html);
                    }
                    return sb.ToString();
                }
            }
        }
        /// <summary>
        /// Returns the completel HTML for this element and its children
        /// </summary>
        public override string Html
        {
            get
            {
                return GetHtml(true);
            }
        }
        /// <summary>
        /// Returns the HTML for this element, ignoring children/innerHTML
        /// </summary>
        public string ElementHtml
        {
            get
            {
                return GetHtml(false);
            }
        }
        protected DocType DocType
        {
            get
            {
                if (_DocType == 0)
                {
                    _DocType = Root == null ? DocType.XHTML : Root.DocType;
                }
                return _DocType;
            }
        }
        private DocType _DocType;
        
        protected string GetHtml(bool includeChildren)
        {
            
            StringBuilder sb = new StringBuilder();
            sb.Append("<" + Tag);
            if (_Classes.Count > 0)
            {
                sb.Append(" class=\"" + Class+"\"");
            }
            if (_Styles.Count > 0)
            {
                sb.Append(" style=\"" + Style+"\"");
            }
            foreach (var kvp in _Attributes)
            {
                string val = kvp.Value;
                if (val != String.Empty)
                {
                    sb.Append(" " + kvp.Key + "=\"" + kvp.Value + "\"");
                }
                else
                {
                    sb.Append(" " + kvp.Key);
                }
            }

            if (InnerHtmlAllowed)
            {
                sb.Append(String.Format(">{0}</" + Tag + ">",
                    includeChildren ? InnerHtml : String.Empty
                    ));
            }
            else
            {
                if (DocType == DocType.XHTML)
                {
                    sb.Append(" />");
                }
                else
                {
                    sb.Append(" >");
                }
            }
            return sb.ToString();
        }
        
        public override string ToString()
        {
            return Html;
        }
        /// <summary>
        /// This object type can have inner HTML.
        /// </summary>
        /// <returns></returns>
        public override bool InnerHtmlAllowed
        {
            get
            {
                switch (Tag.ToLower())
                {
                    case "base":
                    case "basefont":
                    case "frame":
                    case "link":
                    case "meta":
                    case "area":
                    case "col":
                    case "hr":
                    case "param":
                    case "input":
                    case "img":
                    case "br":
                    case "!doctype":
                    case "!--":
                        return false;
                    default:
                        return true;
                }
            }
        }
        public override bool Complete
        {
            get { return !String.IsNullOrEmpty(Tag); }
        }
    }
}
