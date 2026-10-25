// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    //! Marks a component whose own or referenced Transforms are moved at runtime WITHOUT a Drive.
    //!
    //! The MeshCombiner bakes every renderer into the mesh of its nearest "bucket" ancestor, and it
    //! only ever opens buckets for Drive transforms and for KinematicJoint bodies. Anything else -
    //! a ribbon idler turned by its web, a dancer carriage, the scaled roll of a winder - would be
    //! baked into the static root mesh and then visibly stand still while its Transform keeps
    //! moving. Implementing this interface tells the combiner to give each returned Transform a
    //! bucket of its own.
    //!
    //! The interface lives in realvirtual.base so the combiner needs no reference to the assemblies
    //! of the components that use it; the combiner discovers implementations through
    //! GetComponentsInChildren<MonoBehaviour>.
    public interface ICombinerMovingBody
    {
        //! The Transforms this component moves at runtime and that therefore each need their own
        //! combine bucket. Nulls and Transforms outside the combined hierarchy are ignored by the
        //! combiner; returning the component's own transform is the common case.
        IEnumerable<Transform> MovingTransforms { get; }
    }
}
