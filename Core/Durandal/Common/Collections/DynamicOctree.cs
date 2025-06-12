using System;
using System.Collections.Generic;
using System.Linq;
using Durandal.Common.MathExt;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// An octree (3D vector containment structure) that resizes itself dynamically to whatever boundaries it needs
    /// for the objects that it contains.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DynamicOctree<T>
    {
        private OctreeNode<T> root;

        private IDictionary<T, Vector3f> itemLocations;

        //Used to lazily resize the tree based on its data
        private int compressionCounter = 0;
        private const int COMPRESSION_INTERVAL = 100;

        /// Creates a new dynamic quad tree. The tree will span any arbitrary space,
        /// adjusting its size automatically based on its input data
        public DynamicOctree()
            : this(new Cube3f(0, 0, 0, 1, 1, 1))
        {
        }

        /// Creates a new dynamic quad tree, with a hint to indicate its probable
        /// maximum bounds.
        public DynamicOctree(Cube3f initialBounds)
        {
            root = new OctreeNode<T>(this, null, -1);
            itemLocations = new Dictionary<T, Vector3f>();
            root.SetBounds(initialBounds);
        }

        /// Returns the node that is at the specified point, at the lowest level on the tree.
        private OctreeNode<T> GetContainingNode(Vector3f point)
        {
            if (!root.IsSubdivided)
                return root;
            else if (!root.Contains(point))
                return root;
            else
                return root.GetContainingNode(point); //Start the recursion
        }

        /// Finds and returns the smallest node that contains the entire specified cube.
        private OctreeNode<T> GetContainingNode(Cube3f cube)
        {
            if (!root.IsSubdivided) //Case: Root is the only node in the map
                return root;
            else if (!root.Contains(cube))
                return root; //Case: No quad is large enough to contain the cube - return the root
            else
            {
                // Cycle through and call the recursive function on the root's children
                for (int child = 0; child < 8; child++)
                {
                    if (root.GetChild(child).Contains(cube))
                        return root.GetChild(child).GetContainingNode(cube);
                }

                return root; //Case: Root quad contains the rectangle but none of its children do - return root
            }
        }

        /// <summary>
        /// Called privately by the child nodes when an item has been removed. Used to synchronize 
        /// the overall tree's item records with the fact that the item is now gone.
        /// </summary>
        /// <param name="toRemove"></param>
        protected void RemoveItemRecord(T toRemove)
        {
            itemLocations.Remove(toRemove);
        }

        /// <summary>
        /// Increases the size of this tree recursively until it contains the specified point.
        /// </summary>
        /// <param name="target"></param>
        private void ExpandOutTo(Vector3f target)
        {
            while (!root.Contains(target))
            {
                OctreeNode<T> newRoot = new OctreeNode<T>(this, null, -1);
                newRoot.Weight = root.Weight;

                bool right = target.X > root.GetBounds().MaxX;
                bool bottom = target.Y > root.GetBounds().MaxY;
                bool front = target.Z > root.GetBounds().MaxZ;

                if (!right && !bottom && !front) // Up and left
                {
                    root.Parent = newRoot;
                    root.ParentId = 3;
                    newRoot.SetBounds(new Cube3f(
                            root.X - root.Width,
                            root.Y - root.Height,
                            root.Z - root.Depth,
                            root.Width * 2f,
                            root.Height * 2f,
                            root.Depth * 2f));
                    newRoot.Subdivide();
                    newRoot.SetChild(7, root);
                    root = newRoot;
                }
                else if (!right && bottom && !front) // Down and left
                {
                    root.Parent = newRoot;
                    root.ParentId = 1;
                    newRoot.SetBounds(new Cube3f(
                            root.X - root.Width,
                            root.Y,
                            root.Z - root.Depth,
                            root.Width * 2f,
                            root.Height * 2f,
                            root.Depth * 2f));
                    newRoot.Subdivide();
                    newRoot.SetChild(5, root);
                    root = newRoot;
                }
                else if (right && !bottom && !front) //Up and right
                {
                    root.Parent = newRoot;
                    root.ParentId = 2;
                    newRoot.SetBounds(new Cube3f(
                            root.X,
                            root.Y - root.Height,
                            root.Z - root.Depth,
                            root.Width * 2f,
                            root.Height * 2f,
                            root.Depth * 2f));
                    newRoot.Subdivide();
                    newRoot.SetChild(6, root);
                    root = newRoot;
                }
                else if (right && bottom && !front) //Down and right
                {
                    root.Parent = newRoot;
                    root.ParentId = 0;
                    newRoot.SetBounds(new Cube3f(
                            root.X,
                            root.Y,
                            root.Z - root.Depth,
                            root.Width * 2f,
                            root.Height * 2f,
                            root.Depth * 2f));
                    newRoot.Subdivide();
                    newRoot.SetChild(4, root);
                    root = newRoot;
                }
                if (!right && !bottom && front) // Up and left (+z)
                {
                    root.Parent = newRoot;
                    root.ParentId = 7;
                    newRoot.SetBounds(new Cube3f(
                            root.X - root.Width,
                            root.Y - root.Height,
                            root.Z,
                            root.Width * 2f,
                            root.Height * 2f,
                            root.Depth * 2f));
                    newRoot.Subdivide();
                    newRoot.SetChild(3, root);
                    root = newRoot;
                }
                else if (!right && bottom && front) // Down and left (+z)
                {
                    root.Parent = newRoot;
                    root.ParentId = 5;
                    newRoot.SetBounds(new Cube3f(
                            root.X - root.Width,
                            root.Y,
                            root.Z,
                            root.Width * 2f,
                            root.Height * 2f,
                            root.Depth * 2f));
                    newRoot.Subdivide();
                    newRoot.SetChild(1, root);
                    root = newRoot;
                }
                else if (right && !bottom && front) //Up and right (+z)
                {
                    root.Parent = newRoot;
                    root.ParentId = 6;
                    newRoot.SetBounds(new Cube3f(
                            root.X,
                            root.Y - root.Height,
                            root.Z,
                            root.Width * 2f,
                            root.Height * 2f,
                            root.Depth * 2f));
                    newRoot.Subdivide();
                    newRoot.SetChild(2, root);
                    root = newRoot;
                }
                else if (right && bottom && front) //Down and right (+z)
                {
                    root.Parent = newRoot;
                    root.ParentId = 4;
                    newRoot.SetBounds(new Cube3f(
                            root.X,
                            root.Y,
                            root.Z,
                            root.Width * 2f,
                            root.Height * 2f,
                            root.Depth * 2f));
                    newRoot.Subdivide();
                    newRoot.SetChild(0, root);
                    root = newRoot;
                }
            }
        }

        /// <summary>
        /// Checks to see if the tree is much larger than it needs to be, and if so, reduces its size
        /// </summary>
        private void CompressTree()
        {
            if (root.IsSubdivided && compressionCounter++ >= COMPRESSION_INTERVAL)
            {
                compressionCounter = 0;
                // Does one of the root's children hold all of the mass?
                for (int child = 0; child < 8; child++)
                {
                    if (root.GetChild(child).Weight == root.Weight)
                    {
                        // Make that the new root
                        root = root.GetChild(child);
                        root.ParentId = -1;
                        root.Parent = null;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Adds the specified item to the tree at the specified point.
        /// </summary>
        /// <param name="toAdd"></param>
        /// <param name="location"></param>
        public void AddItem(T toAdd, Vector3f location)
        {
            if (itemLocations.ContainsKey(toAdd)) // Don't double-add
            {
                //Console.Error.WriteLine("Can't double-add an item to the tree!");
                return;
            }
            itemLocations[toAdd] = location;
            //Expand the tree to accommodate the new item, if necessary
            if (!root.Contains(location))
            {
                ExpandOutTo(location);
            }
            root.AddItem(new Tuple<T, Vector3f>(toAdd, location));
        }

        /// <summary>
        /// Removes the specified item from the tree.
        /// </summary>
        /// <param name="toRemove"></param>
        public void RemoveItem(T toRemove)
        {
            if (itemLocations.ContainsKey(toRemove))
            {
                // Hone in on the item
                OctreeNode<T> node = GetContainingNode(itemLocations[toRemove]);

                // Do the removal
                node.RemoveItem(toRemove);

                // Compress the tree if we can
                CompressTree();
            }
        }

        /// <summary>
        /// Returns the Point associated with the specified item in the tree, or null 
        /// if the item is not in the tree.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public Vector3f GetLocationOf(T item)
        {
            return itemLocations[item];
        }

        /// <summary>
        /// Returns the collection of all items in this tree, and their respective locations.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Tuple<T, Vector3f>> GetAllItems()
        {
            return root.GetItems();
        }

        /// <summary>
        /// Returns the total number of items in the tree.
        /// </summary>
        /// <returns></returns>
        public int Count
        {
            get
            {
                return root.Weight;
            }
        }

        /// <summary>
        /// Returns the list of all items in the tree that fall within the specified boundaries, 
        /// paired with their tree locations.
        /// </summary>
        /// <param name="boundaries"></param>
        /// <returns></returns>
        public List<Tuple<T, Vector3f>> GetItemsInCube(Cube3f boundaries)
        {
            // Find the node that contains the entire specified rectangle
            OctreeNode<T> headNode = GetContainingNode(boundaries);

            // Get its items
            IEnumerable<Tuple<T, Vector3f>> items = headNode.GetItems();

            // And filter out the ones that are outside the rectangle.
            List<Tuple<T, Vector3f>> returnVal = new List<Tuple<T, Vector3f>>();
            foreach (Tuple<T, Vector3f> item in items)
            {
                if (boundaries.Contains(item.Item2))
                    returnVal.Add(item);
            }

            return returnVal;
        }

        private Vector3f PutVectorInBounds(Vector3f point)
        {
            if (!root.Contains(point))
            {
                if (point.X < root.GetBounds().X)
                    point = new Vector3f(root.GetBounds().X, point.Y, point.Z);
                else if (point.X >= root.GetBounds().MaxX)
                    point = new Vector3f(root.GetBounds().X + root.GetBounds().Width * 0.99f, point.Y, point.Z);

                if (point.Y < root.GetBounds().Y)
                    point = new Vector3f(point.X, root.GetBounds().Y, point.Z);
                else if (point.Y >= root.GetBounds().MaxY)
                    point = new Vector3f(point.X, root.GetBounds().Y + root.GetBounds().Height * 0.99f, point.Z);

                if (point.Z < root.GetBounds().Z)
                    point = new Vector3f(point.X, point.Y, root.GetBounds().Z);
                else if (point.Z >= root.GetBounds().MaxZ)
                    point = new Vector3f(point.X, point.Y, root.GetBounds().Z + root.GetBounds().Depth * 0.99f);
            }

            return point;
        }

        /// <summary>
        /// Returns a handful of items that are near the specified point in the tree.
        /// </summary>
        /// <param name="testPoint"></param>
        /// <returns></returns>
        public IEnumerable<Tuple<T, Vector3f>> GetItemsNearPoint(Vector3f testPoint)
        {
            Vector3f point = testPoint;
            // If the test point it outside of the tree, move it to the inside
            point = PutVectorInBounds(point);
            OctreeNode<T> bottomNode = GetContainingNode(point);
            IEnumerable<Tuple<T, Vector3f>> returnVal = bottomNode.GetItems();
            while (!returnVal.Any() && bottomNode.Parent != null)
            {
                bottomNode = bottomNode.Parent;
                returnVal = bottomNode.GetItems();
            }

            return returnVal;
        }

        /// <summary>
        /// Returns a handful of items that are near the specified point in the tree.
        /// </summary>
        /// <param name="testPoint"></param>
        /// <returns></returns>
        public List<Tuple<float, T>> GetItemsNearPointSorted(Vector3f testPoint)
        {
            Vector3f point = testPoint;
            point = PutVectorInBounds(point);
            OctreeNode<T> bottomNode = GetContainingNode(point);
            List<Tuple<float, T>> returnVal = bottomNode.GetItemsSorted(testPoint);
            while (returnVal.Count == 0 && bottomNode.Parent != null)
            {
                bottomNode = bottomNode.Parent;
                returnVal = bottomNode.GetItemsSorted(testPoint);
            }

            returnVal.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            return returnVal;
        }

        /// <summary>
        /// Returns a handful of items that are near the specified point in the tree, within a certain distance
        /// </summary>
        /// <returns></returns>
        public IList<Tuple<T, Vector3f>> GetItemsNearPoint(Vector3f testPoint, float maxDistance)
        {
            Vector3f point = testPoint;
            point = PutVectorInBounds(point);
            OctreeNode<T> bottomNode = GetContainingNode(point);
            IList<Tuple<T, Vector3f>> returnVal = bottomNode.GetItemsNear(testPoint, maxDistance);
            while (returnVal.Count == 0 && bottomNode.Parent != null)
            {
                bottomNode = bottomNode.Parent;
                returnVal = bottomNode.GetItemsNear(testPoint, maxDistance);
            }
            return returnVal;
        }

        /**
        * This node represents a single subdivided square within a tree.
        * @author Logan Stromberg
        */
        private class OctreeNode<E>
        {
            private const int INITIAL_SUBDIVIDE_THRESHOLD = 100;
            private const int SUBDIVIDE_MULTIPLY_FACTOR = 2;
            private int subdivisionThreshold;
            private DynamicOctree<E> containingTree;
            private OctreeNode<E>[] children;
            private Cube3f bounds;
            private bool isSubdivided;
            private Dictionary<E, Tuple<E, Vector3f>> data;
            private int weight;

            // Cached variables for speed
            private float centerX, centerY, centerZ;

            //Everything but the quad size is initialized here in the constructor.
            public OctreeNode(DynamicOctree<E> tree, OctreeNode<E> par, int ID)
            {
                containingTree = tree;
                Parent = par;
                ParentId = ID;
                isSubdivided = false;
                children = new OctreeNode<E>[8];
                weight = 0;
                data = new Dictionary<E, Tuple<E, Vector3f>>();
                subdivisionThreshold = (par == null ? INITIAL_SUBDIVIDE_THRESHOLD : par.SubdivisionThreshold);
            }

            public void SetBounds(Cube3f newBounds)
            {
                bounds = newBounds;
                centerX = bounds.CenterX;
                centerY = bounds.CenterY;
                centerZ = bounds.CenterZ;
            }

            /**
             * Returns true if this node is divided into 4 child nodes.
             */
            public bool IsSubdivided
            {
                get
                {
                    return isSubdivided;
                }
            }

            public OctreeNode<E> GetChild(int i)
            {
                return children[i];
            }

            public void SetChild(int i, OctreeNode<E> newChild)
            {
                children[i] = newChild;
            }

            /**
             * The "parentID" is the index of this child within its parent quad. For example,
             * if a node were child 0 of its parent, then the child's parentID would be 0.
             * In other words, parent.getChild[child.parentID] = child
             * @return This node's parentID. 
             */
            public int ParentId
            {
                get;
                set;
            }

            /**
             * Returns the quadmapnode that contains this one, or null if this is root.
             * @return 
             */
            public OctreeNode<E> Parent
            {
                get;
                set;
            }

            /// <summary>
            /// The width of this node's bounds
            /// </summary>
            public float Width
            {
                get
                {
                    return bounds.Width;
                }
            }

            /// <summary>
            ///  The height of this node's bounds
            /// </summary>
            /// <returns></returns>
            public float Height
            {
                get
                {
                    return bounds.Height;
                }
            }

            /// <summary>
            /// The depth of this node's bounds
            /// </summary>
            /// <returns></returns>
            public float Depth
            {
                get
                {
                    return bounds.Depth;
                }
            }

            /// <summary>
            /// Returns the weight of this node (which is the count of all objects 
            /// that are within its boundaries)
            /// </summary>
            public int Weight
            {
                get
                {
                    return weight;
                }
                set
                {
                    weight = value;
                }
            }

            /// <summary>
            /// Returns the subdivision threshold, which is the number of items that 
            /// must be within this node before it will trigger subdivision.
            /// </summary>
            private int SubdivisionThreshold
            {
                get
                {
                    return subdivisionThreshold;
                }
            }

            /// <summary>
            /// Returns the coordinate of the x origin of this quad.
            /// </summary>
            public float X
            {
                get
                {
                    return bounds.X;
                }
            }

            /// <summary>
            /// Returns the coordinate of the x origin of this quad.
            /// </summary>
            public float Y
            {
                get
                {
                    return bounds.Y;
                }
            }

            /// <summary>
            /// Returns the coordinate of the x origin of this quad.
            /// </summary>
            public float Z
            {
                get
                {
                    return bounds.Z;
                }
            }

            /// <summary>
            /// Removes a specific item from this node. If the item is not directly
            /// contained in this node (i.e. this is not the lowest-level node)
            /// then this method will do nothing.
            /// </summary>
            /// <param name="toRemove"></param>
            public void RemoveItem(E toRemove)
            {
                if (data != null && data.ContainsKey(toRemove))
                {
                    data.Remove(toRemove);
                    containingTree.RemoveItemRecord(toRemove);

                    //Propagate the weight difference up to the root
                    PropagateWeightChangeUp();
                }
            }

            protected void PropagateWeightChangeUp()
            {
                weight--;
                // Coalesce on the way up
                if (weight < subdivisionThreshold)
                    Coalesce();
                if (Parent != null)
                    Parent.PropagateWeightChangeUp();
            }

            /**
             * Adds the specified item to this node. This method carries the important
             * precondition that this node already contains() the specified point.
             * If necessary, this node will be subdivided to compensate.
             * @param newItem The item to be added, paired with a location.
             */
            public void AddItem(Tuple<E, Vector3f> newItem)
            {
                weight += 1;
                if (!isSubdivided)
                {
                    data[newItem.Item1] = newItem;
                    if (weight > subdivisionThreshold)
                        Subdivide();
                }
                else
                {
                    if (newItem.Item2.Z < centerZ)
                    {
                        if (newItem.Item2.X < centerX)
                        {
                            if (newItem.Item2.Y < centerY)
                                children[0].AddItem(newItem);
                            else
                                children[2].AddItem(newItem);
                        }
                        else
                        {
                            if (newItem.Item2.Y < centerY)
                                children[1].AddItem(newItem);
                            else
                                children[3].AddItem(newItem);
                        }
                    }
                    else
                    {
                        if (newItem.Item2.X < centerX)
                        {
                            if (newItem.Item2.Y < centerY)
                                children[4].AddItem(newItem);
                            else
                                children[6].AddItem(newItem);
                        }
                        else
                        {
                            if (newItem.Item2.Y < centerY)
                                children[5].AddItem(newItem);
                            else
                                children[7].AddItem(newItem);
                        }
                    }
                }
            }

            /**
             * Divides this node into 4 child nodes. All objects in this node will be
             * offloaded onto the children
             */
            public void Subdivide()
            {
                if (!IsSubdivided)
                {
                    // Detect if this node is involved in infinite recursion
                    if (Parent != null && weight == Parent.Weight)
                        subdivisionThreshold = Parent.SubdivisionThreshold * SUBDIVIDE_MULTIPLY_FACTOR;

                    isSubdivided = true;
                    for (int c = 0; c < 8; c++)
                        children[c] = new OctreeNode<E>(containingTree, this, c);

                    float halfWidth = Width / 2f;
                    float halfHeight = Height / 2f;
                    float halfDepth = Depth / 2f;
                    children[0].SetBounds(new Cube3f(X, Y, Z, halfWidth, halfHeight, halfDepth));
                    children[1].SetBounds(new Cube3f(X + halfWidth, Y, Z, halfWidth, halfHeight, halfDepth));
                    children[2].SetBounds(new Cube3f(X, Y + halfHeight, Z, halfWidth, halfHeight, halfDepth));
                    children[3].SetBounds(new Cube3f(X + halfWidth, Y + halfHeight, Z, halfWidth, halfHeight, halfDepth));
                    children[4].SetBounds(new Cube3f(X, Y, Z + halfDepth, halfWidth, halfHeight, halfDepth));
                    children[5].SetBounds(new Cube3f(X + halfWidth, Y, Z + halfDepth, halfWidth, halfHeight, halfDepth));
                    children[6].SetBounds(new Cube3f(X, Y + halfHeight, Z + halfDepth, halfWidth, halfHeight, halfDepth));
                    children[7].SetBounds(new Cube3f(X + halfWidth, Y + halfHeight, Z + halfDepth, halfWidth, halfHeight, halfDepth));

                    // Add this node's items to the children
                    foreach (Tuple<E, Vector3f> item in data.Values)
                    {
                        if (item.Item2.Z < centerZ)
                        {
                            if (item.Item2.X < centerX)
                            {
                                if (item.Item2.Y < centerY)
                                    children[0].AddItem(item);
                                else
                                    children[2].AddItem(item);
                            }
                            else
                            {
                                if (item.Item2.Y < centerY)
                                    children[1].AddItem(item);
                                else
                                    children[3].AddItem(item);
                            }
                        }
                        else
                        {
                            if (item.Item2.X < centerX)
                            {
                                if (item.Item2.Y < centerY)
                                    children[4].AddItem(item);
                                else
                                    children[6].AddItem(item);
                            }
                            else
                            {
                                if (item.Item2.Y < centerY)
                                    children[5].AddItem(item);
                                else
                                    children[7].AddItem(item);
                            }
                        }
                    }

                    //Delete the local storage of this node.
                    data.Clear();
                    data = null;
                }
                //else
                //    Console.Error.WriteLine("Warning! Attempting to subdivide a divided quad!");
            }

            /**
             * If this node has children, recursively delete them all.
             * Opposite of "subdivide".
             */
            private void Coalesce()
            {
                if (isSubdivided)
                {
                    isSubdivided = false;
                    for (int c = 0; c < 8; c++)
                        children[c].Coalesce();

                    // Pull the objects owned by the children into this node
                    data = new Dictionary<E, Tuple<E, Vector3f>>();
                    for (int c = 0; c < 8; c++)
                    {
                        children[c].AddItemsTo(data);
                        children[c] = null;
                    }
                }
            }

            /**
             * Returns true if any part of this quad intersects the specified rectangle.
             */
            public bool Intersects(Cube3f rectangle)
            {
                return bounds.Intersects(rectangle);
            }

            /**
             * Returns true iff this quad contains the entire rectangle.
             * Containment is inclusive on the top and left edges, and exclusive
             * on the right and bottom edges.
             */
            public bool Contains(Cube3f cube)
            {
                return bounds.Contains(cube);
            }

            /**
             * Returns true iff this quad contains the specified point.
             * Containment is inclusive on the top and left edges, and exclusive
             * on the right and bottom edges.
             */
            public bool Contains(Vector3f point)
            {
                return bounds.Contains(point);
            }

            /**
             * Returns the set of all items that are contained within this node and all
             * of its children
             * @return 
             */
            public IEnumerable<Tuple<E, Vector3f>> GetItems()
            {
                if (!isSubdivided)
                {
                    return data.Values;
                }
                else
                {
                    HashSet<Tuple<E, Vector3f>> returnVal = new HashSet<Tuple<E, Vector3f>>();
                    for (int c = 0; c < 8; c++ )
                        children[c].AddItemsTo(returnVal);
                    return returnVal;
                }
            }

            /*
             * ?The last hour is on us both?mr.s?tuck this little kitty into the impenetrable
             * brainpan?
             * pr?Contents under pressure?Do not expose to excessive heat, vacuum, blunt trauma,
             * immersion in liquids, disintegration, reintegration, hypersleep, humiliation, sorrow
             * or harsh language?
             * pr?When the time comes, whose life will flash before yours?
             */

            public IList<Tuple<E, Vector3f>> GetItemsNear(Vector3f point, float maxDist)
            {
                if (!isSubdivided)
                {
                    IList<Tuple<E, Vector3f>> returnVal = new List<Tuple<E, Vector3f>>();
                    foreach (var item in data)
                    {
                        float dist = point.Distance(item.Value.Item2);
                        if (dist < maxDist)
                            returnVal.Add(item.Value);
                    }
                    return returnVal;
                }
                else
                {
                    IList<Tuple<E, Vector3f>> returnVal = new List<Tuple<E, Vector3f>>();
                    for (int c = 0; c < 8; c++)
                        children[c].AddItemsTo(returnVal, point, maxDist);
                    return returnVal;
                }
            }

            /**
             * Returns the set of all items that are contained within this node and all
             * of its children, sorted by their distance from the given vector
             * @return 
             */
            public List<Tuple<float, E>> GetItemsSorted(Vector3f testPoint)
            {
                List<Tuple<float, E>> returnVal = new List<Tuple<float, E>>();
                AddItemsTo(returnVal, testPoint);
                return returnVal;
            }

            /**
             * Adds all of the items in this node and all child nodes to the specified
             * set.
             * @param targetSet 
             */
            private void AddItemsTo(HashSet<Tuple<E, Vector3f>> targetSet)
            {
                if (!isSubdivided)
                {
                    foreach (var val in data.Values)
                    {
                        targetSet.Add(val);
                    }
                }
                else
                {
                    for (int c = 0; c < 8; c++)
                        children[c].AddItemsTo(targetSet);
                }
            }

            /**
             * Adds all of the items in this node and all child nodes to the specified
             * map.
             * @param targetSet 
             */
            private void AddItemsTo(IDictionary<E, Tuple<E, Vector3f>> targetMap)
            {
                if (!isSubdivided)
                {
                    foreach (Tuple<E, Vector3f> item in data.Values)
                        targetMap[item.Item1] = item;
                }
                else
                {
                    for (int c = 0; c < 8; c++)
                        children[c].AddItemsTo(targetMap);
                }
            }

            /**
             * Adds all of the items in this node and all child nodes to the specified
             * sorted list
             * @param targetSet 
             */
            private void AddItemsTo(List<Tuple<float, E>> targetList, Vector3f testPoint)
            {
                if (!isSubdivided)
                {
                    foreach (var val in data.Values)
                    {
                        float dist = testPoint.Distance(val.Item2);
                        targetList.Add(new Tuple<float, E>(dist, val.Item1));
                    }
                }
                else
                {
                    for (int c = 0; c < 8; c++)
                        children[c].AddItemsTo(targetList, testPoint);
                }
            }

            /**
             * Adds all of the items in this node and all child nodes to the specified
             * sorted list
             * @param targetSet 
             */
            private void AddItemsTo(IList<Tuple<E, Vector3f>> targetList, Vector3f testPoint, float maxDist)
            {
                if (!isSubdivided)
                {
                    foreach (var val in data.Values)
                    {
                        float dist = testPoint.Distance(val.Item2);
                        if (dist < maxDist)
                            targetList.Add(val);
                    }
                }
                else
                {
                    for (int c = 0; c < 8; c++)
                        children[c].AddItemsTo(targetList, testPoint, maxDist);
                }
            }

            /**
             * Recursive counterpart to QuadMap.getContainingNode().
             * Finds the lowest node in the tree that contains the rectangle.
             */
            public OctreeNode<E> GetContainingNode(Cube3f cube)
            {
                if (!isSubdivided)
                {
                    return this; //We hit the bottom of the tree - return this quad
                }
                else
                {
                    if (cube.X < bounds.X ||
                            cube.Y < bounds.Y ||
                            cube.Z < bounds.Z ||
                            cube.MaxX >= bounds.MaxX ||
                            cube.MaxY >= bounds.MaxY ||
                            cube.MaxZ >= bounds.MaxZ)
                    {
                        return null; // Rectangle not in this quad; fail
                    }

                    if (cube.MaxZ < centerZ)
                    {
                        if (cube.MaxX < centerX)
                        {
                            if (cube.MaxY < centerY)
                                return children[0].GetContainingNode(cube);
                            else if (cube.Y >= centerY)
                                return children[2].GetContainingNode(cube);
                            else
                                return this;
                        }
                        else if (cube.X >= centerX)
                        {
                            if (cube.MaxY < centerY)
                                return children[1].GetContainingNode(cube);
                            else if (cube.Y >= centerY)
                                return children[3].GetContainingNode(cube);
                            else
                                return this;
                        }
                        else
                        {
                            return this;
                        }
                    }
                    else if (cube.Z >= centerZ)
                    {
                        if (cube.MaxX < centerX)
                        {
                            if (cube.MaxY < centerY)
                                return children[4].GetContainingNode(cube);
                            else if (cube.Y >= centerY)
                                return children[6].GetContainingNode(cube);
                            else
                                return this;
                        }
                        else if (cube.X >= centerX)
                        {
                            if (cube.MaxY < centerY)
                                return children[5].GetContainingNode(cube);
                            else if (cube.Y >= centerY)
                                return children[7].GetContainingNode(cube);
                            else
                                return this;
                        }
                        else
                        {
                            return this;
                        }
                    }
                    else
                    {
                        return this;
                    }
                }
            }

            //Recursive counterpart to QuadMap.getContainingNode()
            public OctreeNode<E> GetContainingNode(Vector3f point)
            {
                if (!isSubdivided)
                    return this; //Case: We hit the lowest tree node that contains the point - this is the one we want
                else
                {
                    if (point.Z < centerZ)
                    {
                        if (point.X < centerX)
                        {
                            if (point.Y < centerY)
                                return children[0].GetContainingNode(point);
                            else if (point.Y < bounds.Y + bounds.Height)
                                return children[2].GetContainingNode(point);
                        }
                        else
                        {
                            if (point.Y < centerY)
                                return children[1].GetContainingNode(point);
                            else if (point.Y < bounds.Y + bounds.Height)
                                return children[3].GetContainingNode(point);
                        }
                    }
                    else if (point.Z >= centerZ)
                    {
                        if (point.X < centerX)
                        {
                            if (point.Y < centerY)
                                return children[4].GetContainingNode(point);
                            else if (point.Y < bounds.Y + bounds.Height)
                                return children[6].GetContainingNode(point);
                        }
                        else
                        {
                            if (point.Y < centerY)
                                return children[5].GetContainingNode(point);
                            else if (point.Y < bounds.Y + bounds.Height)
                                return children[7].GetContainingNode(point);
                        }
                    }
                }
                return null; // Point not within this quad
            }

            /// <summary>
            /// Returns the boundary rectangle of this node.
            /// </summary>
            /// <returns></returns>
            public Cube3f GetBounds()
            {
                return bounds;
            }
        }
    }
}
