using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
﻿using UnityEngine;

namespace Phobos.Looting.Utilities
{
    public class ColliderDistanceComparer(Vector3 referencePosition) : IComparer<Collider>
    {
        public int Compare(Collider x, Collider y)
        {
            float distX = Vector3.SqrMagnitude(x.bounds.center - referencePosition);
            float distY = Vector3.SqrMagnitude(y.bounds.center - referencePosition);
            return distX.CompareTo(distY);
        }
    }
}
