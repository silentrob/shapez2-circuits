using Game.Core.Coordinates;
using UnityEngine;

namespace Circuits.Kit
{
    // A building's click box from its mesh's bounds. ShapezShifter's BoundingBoxHelper converts the bounds' size
    // from Unity's axes (y up, z north) to the game's (y south, z up) but not their centre, so a mesh centred
    // away from its origin tile (a 4x1 pin) gets a box sunk below the floor, which the cursor barely reaches.
    public static class ClickBox
    {
        public static CollisionBox[] For(Mesh mesh)
        {
            Vector3 centre = mesh.bounds.center;
            return new[]
            {
                new CollisionBox(new SerializedCollisionBox
                {
                    Center_L = new LocalVector(centre.x, -centre.z, centre.y),
                    Dimensions_L = mesh.bounds.size
                })
            };
        }
    }
}
