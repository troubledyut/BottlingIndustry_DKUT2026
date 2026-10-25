// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz

namespace realvirtual
{
    #region doc
    //! Collision role of a node for the collision detection of realvirtual WEB (3D-HMI).

    //! The role classifies what a body represents in the machine so that realvirtual WEB can
    //! decide which pairs of bodies are checked against each other. Bodies carrying the SAME
    //! role never collide with each other; only different roles form a collision pair.
    //!
    //! The value is exported into the GLB as the plain enum NAME (for example "Workpiece") and
    //! is read by realvirtual WEB. The names and their order are therefore a contract and must
    //! stay wordwise identical to COLLISION_ROLES in the WebViewer
    //! (Assets/realvirtual-WebViewer~/src/core/engine/rv-collision-role.ts) — see plan-394.
    //!
    //! New roles are only ever APPENDED at the end, never inserted or reordered (plan-409 rule);
    //! Cutter was appended for exactly that reason. A cutting tool gets its own role instead of
    //! reusing Tool because identical roles never collide — a tool changer gripper (Tool) must
    //! still be checked against the milling cutter it grabs.
    //!
    //! None is a valid, exported value and simply produces no effective collision pairs.
    #endregion
    public enum WebCollisionRoleType
    {
        None,        //!< No role - the node takes part in no collision pair
        Tool,        //!< Tool, gripper or end effector
        Workpiece,   //!< Part / product being handled or machined
        Machine,     //!< Static or moving machine structure (doors, guards, frames)
        Robot,       //!< Robot arm or kinematic manipulator
        Environment, //!< Surrounding environment (walls, fences, floor)
        Cutter       //!< Cutting tool (milling cutter, saw blade) - separate from Tool by design
    }
}
