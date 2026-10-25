// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

using System.Collections.Generic;
using UnityEngine;

namespace realvirtual
{
    //! Marks a component that holds direct references to Renderers which must survive mesh combining.
    //!
    //! The MeshCombiner bakes every renderer into the mesh of its nearest bucket ancestor and then
    //! disables it; PerformanceOptimizer.FinalizeOptimization afterwards destroys the MeshRenderer and
    //! the MeshFilter, and CleanUpHierarchy deletes the emptied GameObject. A component that keeps a
    //! hand authored reference to that Renderer - a SceneSelectable pointing at the parts a user can
    //! pick and highlight - loses that reference for good. Implementing this interface takes the
    //! returned Renderers out of the combine entirely: they are never baked into another mesh, never
    //! disabled and never destroyed, so the optimized scene still highlights and picks them.
    //!
    //! Unlike ICombinerMovingBody the combiner collects these scene wide and not below the combined
    //! root: the component holding the references may live anywhere - a SceneSelectable sits in the
    //! SceneSelectionManager's UI hierarchy and only its renderers reach into the machine.
    //!
    //! The interface lives in realvirtual.base so the combiner needs no reference to the assemblies
    //! of the components that use it.
    public interface ICombinerProtectedRenderers
    {
        //! The Renderers that must stay untouched by the combiner. Nulls and Renderers outside the
        //! combined hierarchy are ignored.
        IEnumerable<Renderer> ProtectedRenderers { get; }
    }
}
