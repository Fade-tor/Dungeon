using UnityEngine;
using System.Collections.Generic;

public class RoomDecorator : MonoBehaviour
{
    [System.Serializable]
    public class SpawnableGroup
    {
        [Header("datos")]
        public string name; // Ej: "Mesas", "Cofres", "Lámparas"
        public GameObject[] prefabs; // Prefabs a instanciar (se elige uno al azar)

        [Header("Cantidad")]
        public int minCount = 1;
        public int maxCount = 3;

        [Header("Zonas de Spawn")]
        [Range(0f, 1f)] public float minDistanceFromCenter = 0f;
        [Range(0f, 1f)] public float maxDistanceFromCenter = 1f;
        public Vector2 heightRange = new Vector2(0f, 0f);

        [Header("Rotación")]
        public bool faceCenter = false;
        public bool alignWithWalls = false; //Para usar con cuadros y luces
        public Vector3 rotatioOffset = Vector3.zero;

        [Header("colisiones")]
        public bool avoidOverlap = true; // Evita que se superpongan con otros objetos
        public float overlapRadius = 1f; // Radio para detectar superposición
    }

    [Header("Configuración")]
    public Vector2 roomSize = new Vector2(10f, 10f); // Tamaño en X/Z (asume Y es altura)
    public LayerMask obstacleLayers; // Capas que marcan obstáculos (paredes, otros muebles)
    public bool visualizeInEditor = true;

    [Header("Grupos de Objetos")]
    public SpawnableGroup[] spawnableGroups;

    void Start() => DecorateRoom();

    public void DecorateRoom()
    {
        List<Vector3> usedPositions = new List<Vector3>();

        foreach (var group in spawnableGroups)
        {
            int count = Random.Range(group.minCount, group.maxCount + 1);

            for (int i = 0; i < count; i++)
            {
                Vector3? spawnPos = FindValidSpawnPosition(group, usedPositions);
                if (spawnPos.HasValue)
                {
                    Quaternion rotation = CalculateRotation(group, spawnPos.Value);
                    GameObject prefab = group.prefabs[Random.Range(0, group.prefabs.Length)];
                    GameObject newObj = Instantiate(prefab, spawnPos.Value, rotation, transform);

                    //Ajusto altura post instanciado
                    Vector3 pos = newObj.transform.position;
                    pos.y += Random.Range(group.heightRange.x, group.heightRange.y);
                    newObj.transform.position = pos;

                    usedPositions.Add(spawnPos.Value);
                }
            }
        }
    }

    private Vector3? FindValidSpawnPosition(SpawnableGroup group, List<Vector3> usedPositions)
    {
        int attempts = 50; // Evitar bucles infinitos

        while (attempts > 0)
        {
            // Calcular posición aleatoria dentro de los límites
            float distanceFromCenter = Random.Range(group.minDistanceFromCenter, group.maxDistanceFromCenter);
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            Vector3 pos = new Vector3(
               randomDir.x * roomSize.x * 0.5f * distanceFromCenter,
               0,
               randomDir.y * roomSize.y * 0.5f * distanceFromCenter
             ) + transform.position;

            //forzar alineación con paredes si esta activado
            if (group.alignWithWalls)
            {
                pos = AlignToNearestWall(pos, group);
            }

            // Verificar superposición si es necesario
            if (!group.avoidOverlap || !CheckOverlap(pos, group.overlapRadius, usedPositions))
            {
                return pos;
            }

            attempts--;
        }
        //mensaje de debuggeo para cuando no encuentra posición
        Debug.LogWarning($"No se encontró posición válida para {group.name} después de 50 intentos");
        return null;
    }

    private Vector3 AlignToNearestWall(Vector3 position, SpawnableGroup group)
    {
        //determino que pared está mas cerca
        Vector3 toCenter = transform.position - position;
        bool closerToVerticalWall = Mathf.Abs(toCenter.x) > Mathf.Abs(toCenter.z);

        if (closerToVerticalWall)
        {
            //alinear con pared este/oeste
            position.z = transform.position.x + (toCenter.x > 0 ? -1 : 1) * roomSize.x * 0.5f;
        }
        else
        {
            //alinear con pared norte/sur
            position.z = transform.position.z + (toCenter.z > 0 ? -1 : 1) * roomSize.y * 0.5f;
        }

        //ajuste fino para evitar clipping
        float paddind = group.overlapRadius * 0.5f;
        position.x = Mathf.Clamp(position.x, transform.position.x - roomSize.x * 0.5f + paddind, transform.position.x + roomSize.x * 0.5f - paddind);
        position.z = Mathf.Clamp(position.z, transform.position.z - roomSize.y * 0.5f + paddind, transform.position.y + roomSize.y * 0.5f - paddind);

        return position;
    }

    private Quaternion CalculateRotation(SpawnableGroup group, Vector3 position)
    {
        Quaternion rotation = Quaternion.identity;

        if (group.faceCenter)
        {
            rotation = Quaternion.LookRotation(transform.position - position);
        }
        else if (group.alignWithWalls)
        {
            Vector3 toCenter = transform.position - position;
            rotation = Quaternion.LookRotation(toCenter.normalized);
        }

        //Aplicar offset personalizado
        return rotation * Quaternion.Euler(group.rotatioOffset);
    }

    private bool CheckOverlap(Vector3 position, float radius, List<Vector3> usedPositions)
    {
        if (Physics.CheckSphere(position, radius, obstacleLayers)) return true;

        foreach (var usedPos in usedPositions)
        {
            if (Vector3.Distance(position, usedPos)  < radius) return true;
        }
        return false;
    }

    // Visualización en el Editor
    private void OnDrawGizmosSelected()
    {
        if (!visualizeInEditor || spawnableGroups == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, new Vector3(roomSize.x, 0.1f, roomSize.y));

        foreach (var group in spawnableGroups)
        {
            // Dibujar anillos para las zonas de spawn
            Gizmos.color = Color.Lerp(Color.green, Color.red, group.minDistanceFromCenter);
            float innerRadiusX = roomSize.x * 0.5f * group.minDistanceFromCenter;
            float innerRadiusZ = roomSize.y * 0.5f * group.minDistanceFromCenter;
            Gizmos.DrawWireCube(transform.position, new Vector3(innerRadiusX * 2f, 0.1f, innerRadiusZ * 2f));

            Gizmos.color = Color.Lerp(Color.green, Color.red, group.maxDistanceFromCenter);
            float outerRadiusX = roomSize.x * 0.5f * group.maxDistanceFromCenter;
            float outerRadiusZ = roomSize.y * 0.5f * group.maxDistanceFromCenter;
            Gizmos.DrawWireCube(transform.position, new Vector3(outerRadiusX * 2f, 0.1f, outerRadiusZ * 2f));
        }
    }
}