// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

namespace realvirtual
{
    //! Marks a component whose meshes must stay a unit of their own when meshes are combined.
    //!
    //! The MeshCombiner bakes every renderer into the mesh of its nearest "bucket" ancestor - without
    //! this marker usually the static root mesh. A component that works on "all meshes below me" at
    //! runtime - a RuntimeInteractable that highlights and picks them - then finds nothing left to
    //! work on. Implementing this interface gives the component's transform a bucket of its own: the
    //! meshes below it are still combined, but into a separate mesh that stays below the component.
    //!
    //! The combined mesh is always placed on a child GameObject and never on the component's own
    //! GameObject, so undoing the optimization removes only that child.
    //!
    //! The interface lives in realvirtual.base so the combiner needs no reference to the assemblies
    //! of the components that use it.
    public interface ICombinerBucket
    {
    }
}
