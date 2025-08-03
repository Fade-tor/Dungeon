using UnityEngine;
using System.Collections.Generic;

public class AdvancedRoomDecorator : MonoBehaviour
{
    [System.Serializable]
    public class SpawnableGroup
    {
        [Header("Datos")]
        public string name; // Ej: "Mesas", "Cofres", "Lámparas"
        public GameObject[] prefabs; // Prefabs a instanciar (se elige uno al azar)

        [Header("Cantidad")]
        public int minCount = 1;
        public int maxCount = 3;

        [Header("Zonas de Spawn 3D")]
        public Vector3 spawnAreaSize = new Vector3(10f, 0f, 10f); // Tamaño del área de spawn (0 en Y para altura fija)
        public Vector3 spawnAreaOffset = Vector3.zero; // Offset desde el centro de la habitación
        [Range(0f, 1f)] public float minDistanceFromCenter = 0f;
        [Range(0f, 1f)] public float maxDistanceFromCenter = 1f;
        public Vector2 heightRange = new Vector2(0f, 2f);

        [Header("Rotación")]
        public bool faceCenter = false;
        public bool alignWithWalls = false; // Para usar con cuadros y luces
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Colisiones")]
        public bool avoidOverlap = true; // Evita que se superpongan con otros objetos
        public float overlapRadius = 1f; // Radio para detectar superposición

        [Header("Visualización")]
        public Color gizmoColor = new Color(0.5f, 0.5f, 1f, 0.3f);
        public bool showDistanceRings = true;
    }

    [Header("Configuración")]
    [Tooltip("Tamaño de la habitación (ancho, alto, profundidad). El (0,0,0) es el suelo")]
    public Vector3 roomSize = new Vector3(10f, 3f, 10f); // Tamaño en X/Y/Z
    public LayerMask obstacleLayers; // Capas que marcan obstáculos (paredes, otros muebles)
    public bool visualizeInEditor = true;

    [Tooltip("Offset desde el suelo para el cálculo de la habitación")]
    public Vector3 roomOffset = Vector3.zero;

    [Header("Grupos de Objetos")]
    public SpawnableGroup[] spawnableGroups;

    void Start() => DecorateRoom();

    public void DecorateRoom()
    {
        Debug.Log("Iniciando decoración..."); // Verifica que el método se llame

        List<Vector3> usedPositions = new List<Vector3>();

        foreach (var group in spawnableGroups)
        {
            Debug.Log($"Procesando grupo: {group.name}");
            int count = Random.Range(group.minCount, group.maxCount + 1);
            Debug.Log($"Intentando instanciar {count} objetos");

            for (int i = 0; i < count; i++)
            {
                Vector3? spawnPos = FindValidSpawnPosition(group, usedPositions);
                if (spawnPos.HasValue)
                {
                    Debug.Log($"Encontrada posición válida en {spawnPos.Value}");
                    Quaternion rotation = CalculateRotation(group, spawnPos.Value);
                    GameObject prefab = group.prefabs[Random.Range(0, group.prefabs.Length)];
                    GameObject newObj = Instantiate(prefab, spawnPos.Value, rotation, transform);

                    // Ajusto altura post instanciado
                    Vector3 localPos = newObj.transform.localPosition; //cambiamos transform.position x localPosition para que acomañe al padre
                    localPos.y = Random.Range(group.heightRange.x, group.heightRange.y);
                    newObj.transform.localPosition = localPos;

                    usedPositions.Add(spawnPos.Value);
                }
                else //agrego un else para ver donde esta el lio
                {
                    Debug.LogWarning("No se encontró posición válida");
                }
            }
        }
    }

    private Vector3? FindValidSpawnPosition(SpawnableGroup group, List<Vector3> usedPositions)
    {
        int attempts = 50; // PAra evitar bucles infinitos

        while (attempts > 0)
        {
            //Genero dirección aleatoria
            Vector2 randomDir = Random.insideUnitCircle.normalized;

            //Calculo distancia entre min y max aleatoria
            float distance = Random.Range(group.minDistanceFromCenter, group.maxDistanceFromCenter);


            // Calcular posición aleatoria dentro del área específica del grupo
            Vector3 localPos = new Vector3(
                randomDir.x * group.spawnAreaSize.x * 0.5f * distance,
                0,
                randomDir.y * group.spawnAreaSize.z * 0.5f * distance
            ) + group.spawnAreaOffset; //agrego aca el offset para que ya este aplicado despues

            // Convertir a posición global con offset considerando la rotación del padre
            Vector3 worldPos = transform.TransformPoint(localPos);
            //pos.y = transform.position.y; //a ver si es necesario o no esto

            // Forzar alineación con paredes si está activado
            if (group.alignWithWalls)
            {
                worldPos = AlignToNearestWall(worldPos, group);
            }

            Vector3 checkPos = transform.InverseTransformPoint(worldPos) - roomOffset;

            // Verificar que esté dentro de los límites de la habitación
            if (!IsInsideRoomBounds(checkPos))
            {
                attempts--;
                continue;
            }

            // Verificar superposición si es necesario
            if (!group.avoidOverlap || !CheckOverlap(worldPos, group.overlapRadius, usedPositions))
            {
                return worldPos;
            }

            attempts--;
        }

        Debug.LogWarning($"No se encontró posición válida para {group.name} después de 50 intentos");
        return null;
    }

    private bool IsInsideRoomBounds(Vector3 localPosition)
    {
        //Vector3 localPos = position - transform.position - roomOffset;

        return Mathf.Abs(localPosition.x) <= roomSize.x * 0.5f &&
               localPosition.y >= 0 && localPosition.y <= roomSize.y &&
               Mathf.Abs(localPosition.z) <= roomSize.z * 0.5f;
    }

    private Vector3 AlignToNearestWall(Vector3 position, SpawnableGroup group)
    {
        //agrego conversión a local
        Vector3 localPos = transform.InverseTransformPoint(position);
        //Vector3 toCenter = transform.position - position;
        bool closerToVerticalWall = Mathf.Abs(localPos.x) > Mathf.Abs(localPos.z);

        if (closerToVerticalWall)
        {
            // Alinear con pared este/oeste
            localPos.x = Mathf.Sign(localPos.x) * roomSize.x * 0.5f;
            // position.x = transform.position.x + (localPos.x > 0 ? -1 : 1) * roomSize.x * 0.5f;
        }
        else
        {
            // Alinear con pared norte/sur
            localPos.z = Mathf.Sign(localPos.z) * roomSize.z * 0.5f;
            //position.z = transform.position.z + (localPos.z > 0 ? -1 : 1) * roomSize.z * 0.5f;
        }

        // Ajuste fino para evitar clipping
        float padding = group.overlapRadius * 0.5f;
        /*
        position.x = Mathf.Clamp(position.x, transform.position.x - roomSize.x * 0.5f + padding,
                                transform.position.x + roomSize.x * 0.5f - padding);
        position.z = Mathf.Clamp(position.z, transform.position.z - roomSize.z * 0.5f + padding,
                                transform.position.z + roomSize.z * 0.5f - padding);
        */
        localPos.x = Mathf.Clamp(localPos.x, -roomSize.x * 0.5f + padding, roomSize.x * 0.5f - padding);
        localPos.z = Mathf.Clamp(localPos.z, -roomSize.z * 0.5f + padding, roomSize.z * 0.5f - padding);

        return transform.TransformPoint(localPos);
    }

    private Quaternion CalculateRotation(SpawnableGroup group, Vector3 position)
    {
        Vector3 localPos = transform.InverseTransformPoint(position);
        Quaternion rotation = Quaternion.identity;

        if (group.faceCenter)
        {
            Vector3 directionToCenter = -localPos.normalized;
            directionToCenter.y = 0;
            rotation = Quaternion.LookRotation(directionToCenter);
        }
        else if (group.alignWithWalls)
        {
            Vector3 toCenter = -localPos;
            toCenter.y = 0;
            rotation = Quaternion.LookRotation(toCenter.normalized);
        }

        // Aplicar offset personalizado
        return transform.rotation * rotation * Quaternion.Euler(group.rotationOffset);
    }

    private bool CheckOverlap(Vector3 position, float radius, List<Vector3> usedPositions)
    {
        if (Physics.CheckSphere(position, radius, obstacleLayers)) return true;

        foreach (var usedPos in usedPositions)
        {
            if (Vector3.Distance(position, usedPos) < radius) return true;
        }
        return false;
    }

    // Visualización en el Editor
    private void OnDrawGizmosSelected()
    {
        if (!visualizeInEditor || spawnableGroups == null) return;

        // Dibujar límites de la habitación
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Vector3 roomCenter = transform.position + roomOffset + Vector3.up * roomSize.y * 0.5f;
        Gizmos.DrawWireCube(roomCenter, roomSize);

        foreach (var group in spawnableGroups)
        {
            // Dibujar área de spawn del grupo
            Gizmos.color = group.gizmoColor;

            // Calcular posición del área de spawn con offset
            Vector3 spawnAreaCenter = transform.position + group.spawnAreaOffset;

            Vector3 spawnAreaVisualSize = new Vector3(group.spawnAreaSize.x, 
                                            group.spawnAreaSize.y > 0 ? group.spawnAreaSize.y : 0.1f, 
                                            group.spawnAreaSize.z);
            /*
            // Dibujar cubo que representa el área de spawn
            //Gizmos.DrawWireCube(spawnAreaCenter, group.spawnAreaSize); // codigo anterior
            Gizmos.DrawWireCube(spawnAreaCenter + Vector3.up * spawnAreaVisualSize.y * 0.5f, spawnAreaCenter);
            Gizmos.color = new Color(group.gizmoColor.r, group.gizmoColor.g, group.gizmoColor.b, 0.5f);
            //Gizmos.DrawCube(spawnAreaCenter, group.spawnAreaSize); //codigo anterior
            Gizmos.DrawCube(spawnAreaCenter + Vector3.up * spawnAreaVisualSize.y * 0.5f, spawnAreaCenter);

            // Dibujar rango de distancia desde el centro
            
            Gizmos.color = Color.Lerp(Color.green, Color.red, group.minDistanceFromCenter);
            Gizmos.DrawWireSphere(spawnAreaCenter, group.minDistanceFromCenter * group.spawnAreaSize.magnitude * 0.5f);

            Gizmos.color = Color.Lerp(Color.green, Color.red, group.maxDistanceFromCenter);
            Gizmos.DrawWireSphere(spawnAreaCenter, group.maxDistanceFromCenter * group.spawnAreaSize.magnitude * 0.5f);
            */ //Código Anterior
            if (group.showDistanceRings)
            {
                //Anillo interno
                Gizmos.color = Color.Lerp(Color.green, Color.red, group.minDistanceFromCenter);
                Vector3 innerSize = new Vector3(
                    group.spawnAreaSize.x * group.minDistanceFromCenter,
                    group.spawnAreaSize.y,
                    group.spawnAreaSize.z * group.minDistanceFromCenter);
                Gizmos.DrawWireCube(spawnAreaCenter + Vector3.up * 0.025f, innerSize);

                //Anillo externo
                Gizmos.color = Color.Lerp(Color.green, Color.red, group.maxDistanceFromCenter);
                Vector3 outerSize = new Vector3(
                    group.spawnAreaSize.x * group.maxDistanceFromCenter,
                    group.spawnAreaSize.y,
                    group.spawnAreaSize.z * group.maxDistanceFromCenter);
                Gizmos.DrawWireCube(spawnAreaCenter + Vector3.up * 0.025f, outerSize);
            }
        }
    }
}