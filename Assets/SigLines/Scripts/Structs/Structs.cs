using UnityEngine;

namespace SigLines
{
    struct Node
    {
        public Vector3 position;
        public Vector3 velocity;
    }
    // stride: 24

    struct Line
    {
        public int startNodeIndex;
        public int endNodeIndex;
        public float distance;
    }
    // stride: 12

    struct Parameters 
    {
        public int numOfLinesPerParticle;
        public int numOfNodes;
        public float simulationSpeed;
        public float boundingSphereRad;
        public float deltaTime;
        public float lineMinDist;
    }
    // stride: 24
}