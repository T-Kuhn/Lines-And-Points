using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SigLines
{
    // - Singleton -
    /// <summary>
    /// Handles the compute shader. 
    /// </summary>
    public class ComputeParticles : MonoBehaviour
    {
        static ComputeParticles instance;
        public static ComputeParticles Instance { get { return instance; } }

        [Tooltip("Compute Shader")]
        [SerializeField]
        ComputeShader shader;

        [Tooltip("Number of Particles to create")]
        [Range(1, 10000)]
        [SerializeField]
        int count = 1;

        [Tooltip("Number Lines Per Particle")]
        [Range(1, 100)]
        [SerializeField]
        int numOfLinesPerParticle = 1;

        [Tooltip("Number Lines Per Particle")]
        [Range(1, 1000)]
        [SerializeField]
        int boundingSphereRad = 100;

        [Tooltip("Start Position Range Multiplier")]
        [Range(1, 100)]
        [SerializeField]
        int positionMultiplier = 10;

        [Tooltip("Start velocity Multiplier")]
        [Range(1, 100)]
        [SerializeField]
        int velocityMultiplier = 10;

        [Tooltip("Speed of the Simulation")]
        [Range(0.1f, 5.0f)]
        [SerializeField]
        float simulationSpeed = 1;

        [Tooltip("Minimum Distance required between two points for a line to form")]
        [Range(0.1f, 100.0f)]
        [SerializeField]
        float lineMinDist = 1;

        int kernelHandle;

        // Arrays
        Node[] nodeData;
        Line[] lineData;
        Parameters[] parametersData;

        // buffers
        ComputeBuffer nodeBuffer;
        ComputeBuffer lineBuffer;
        ComputeBuffer parametersBuffer;

        [SerializeField] Mesh particleMesh;
        [SerializeField] Material particleMaterial;
        [SerializeField] Mesh lineMesh;
        [SerializeField] Material lineMaterial;

        MaterialPropertyBlock props;

        ComputeBuffer particleArgsBuffer;
        ComputeBuffer lineArgsBuffer;
        uint[] particleArgs = new uint[5] { 0, 0, 0, 0, 0 };
        uint[] lineArgs = new uint[5] { 0, 0, 0, 0, 0 };

        // - - Init - - 
        /// <summary>
        /// Awake
        /// </summary>
        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                // NOTE: Only one ComputeParticles object is allowed to exist!
                Destroy(gameObject);
            }

            // grab a reference to the computer shader method
            kernelHandle = shader.FindKernel("CSMain");

            nodeData = new Node[count];
            int ct = 0;
            while (ct < nodeData.Length)
            {
                nodeData[ct] = GetNewNode();

                ct++;
            }

            lineData = new Line[count * numOfLinesPerParticle];
            ct = 0;
            while (ct < lineData.Length)
            {
                lineData[ct] = GetNewLine();

                ct++;
            }

            parametersData = new Parameters[1];
            parametersData[0] = GetNewParameters();
            
            setupBuffers();
            setupArgsBuffers();

            particleMaterial.SetBuffer("nodeBuffer", nodeBuffer);
            lineMaterial.SetBuffer("nodeBuffer", nodeBuffer);
            lineMaterial.SetBuffer("lineBuffer", lineBuffer);
            lineMaterial.SetBuffer("parametersBuffer", parametersBuffer);
        }

        // - - Init - - 
        /// <summary>
        /// Setting up node, line and parameter buffers.
        /// Those Buffers are both used in the computeShader and also
        /// in the particle and line draw shaders (Graphics.DrawMeshInstancedIndirect)
        /// </summary>
        void setupBuffers()
        {
            if (nodeBuffer != null) { nodeBuffer.Release(); }
            if (lineBuffer != null) { lineBuffer.Release(); }
            if (parametersBuffer != null) { parametersBuffer.Release(); }

            // create buffers
            nodeBuffer = new ComputeBuffer(nodeData.Length, 24);
            lineBuffer = new ComputeBuffer(lineData.Length, 12);
            parametersBuffer = new ComputeBuffer(1, 24);

            nodeBuffer.SetData(nodeData);
            lineBuffer.SetData(lineData);
            parametersBuffer.SetData(parametersData);

            shader.SetBuffer(kernelHandle, "nodeBuffer", nodeBuffer);
            shader.SetBuffer(kernelHandle, "lineBuffer", lineBuffer);
            shader.SetBuffer(kernelHandle, "parametersBuffer", parametersBuffer);
        }

        // - - Init - -
        /// <summary>
        /// Setting up args buffers for the Graphics.DrawMeshInstancedIndirect draw call on lines and paricles.
        /// </summary>
        void setupArgsBuffers()
        {
            particleArgsBuffer = new ComputeBuffer(1, particleArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            // indirect args
            uint numIndicesPM = (particleMesh != null) ? (uint)particleMesh.GetIndexCount(0) : 0;
            particleArgs[0] = numIndicesPM;
            particleArgs[1] = (uint)nodeData.Length;
            particleArgsBuffer.SetData(particleArgs);

            lineArgsBuffer = new ComputeBuffer(1, lineArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            // indirect args
            uint numIndicesLM = (lineMesh != null) ? (uint)lineMesh.GetIndexCount(0) : 0;
            lineArgs[0] = numIndicesLM;
            lineArgs[1] = (uint)(lineData.Length);
            lineArgsBuffer.SetData(lineArgs);
        }

        // - - Init - -
        /// <summary>
        /// create a new Node object and return it
        /// </summary>
        Node GetNewNode()
        {
            var n = new Node();

            n.position = Random.insideUnitSphere * positionMultiplier;
            n.velocity = Random.insideUnitSphere * velocityMultiplier;

            return n;
        }

        Line GetNewLine()
        {
            var n = new Line();

            n.startNodeIndex = -1;
            n.endNodeIndex = -1;
            n.distance = 0;

            return n;
        }

        // - - Init - -
        /// <summary>
        /// create a new Parameters object and return it
        /// </summary>
        Parameters GetNewParameters()
        {
            var n = new Parameters();

            n.numOfLinesPerParticle = numOfLinesPerParticle;
            n.numOfNodes = count;
            n.simulationSpeed = simulationSpeed;
            n.boundingSphereRad = boundingSphereRad;
            n.deltaTime = Time.deltaTime;
            n.lineMinDist = lineMinDist;

            return n;
        }

        /// <summary>
        /// Update
        /// </summary>
        void Update()
        {
            // get the newest simulation parameters
            parametersData[0] = GetNewParameters();

            // set buffer data
            parametersBuffer.SetData(parametersData);

            // set buffers
            shader.SetBuffer(kernelHandle, "parametersBuffer", parametersBuffer);

            // run compute shader
            shader.Dispatch(kernelHandle, count, 1, 1);

            // Draw the template mesh with instancing.
            if (props == null) props = new MaterialPropertyBlock();

            // Draw particles
            Graphics.DrawMeshInstancedIndirect(
                particleMesh, 
                0, 
                particleMaterial, 
                new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), 
                particleArgsBuffer,
                0, 
                props, 
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false);

            // Draw lines 
            Graphics.DrawMeshInstancedIndirect(
                lineMesh, 
                0, 
                lineMaterial, 
                new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), 
                lineArgsBuffer,
                0, 
                props, 
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false);
        }

        /// <summary>
        /// Delete buffers on destroy
        /// </summary>
        void OnDestroy()
        {
            if (particleArgsBuffer != null) { particleArgsBuffer.Release(); }
            if (lineArgsBuffer != null) { lineArgsBuffer.Release(); }
            if (nodeBuffer != null) { nodeBuffer.Release(); }
            if (lineBuffer != null) { lineBuffer.Release(); }
            if (parametersBuffer != null) { parametersBuffer.Release(); }
        }
    }
}