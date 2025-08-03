using UnityEngine;
using System.Collections.Generic;

public class AdvancedRoomDecorator : MonoBehaviour
{
    [System.Serializable]
    public class SpawnableGroup
    {
        [Header("Datos")]
        public string name; // Ej: "Mesas", "Cofres", "L�mparas"
        public GameObject[] prefabs; // Prefabs a instanciar (se elige uno al azar)

        [Header("Cantidad")]
        public int minCount = 1;
        public int maxCount = 3;

        [Header("Zonas de Spawn 3D")]
        public Vector3 spawnAreaSize = new Vector3(10f, 0f, 10f); // Tama�o del �rea de spawn (0 en Y para altura fija)
        public Vector3 spawnAreaOffset = Vector3.zero; // Offset desde el centro de la habitaci�n
        [Range(0f, 1f)] public float minDistanceFromCenter = 0f;
        [Range(0f, 1f)] public float maxDistanceFromCenter = 1f;
        public Vector2 heightRange = new Vector2(0f, 2f);

        [Header("Rotaci�n")]
        public bool faceCenter = false;
        public bool alignWithWalls = false; // Para usar con cuadros y luces
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Colisiones")]
        public bool avoidOverlap = true; // Evita que se superpongan con otros objetos
        public float overlapRadius = 1f; // Radio para detectar superposici�n

        [Header("Visualizaci�n")]
        public Color gizmoColor = new Color(0.5f, 0.5f, 1f, 0.3f);
        public bool showDistanceRings = true;
    }

    [Header("Configuraci�n")]
    [Tooltip("Tama�o de la habitaci�n (ancho, alto, profundidad). El (0,0,0) es el suelo")]
    public Vector3 roomSize = new Vector3(10f, 3f, 10f); // Tama�o en X/Y/Z
    public LayerMask obstacleLayers; // Capas que marcan obst�culos (paredes, otros muebles)
    public bool visualizeInEditor = true;

    [Tooltip("Offset desde el suelo para el c�lculo de la habitaci�n")]
    public Vector3 roomOffset = Vector3.zero;

    [Header("Grupos de Objetos")]
    public SpawnableGroup[] spawnableGroups;

    void Start() => DecorateRoom();

    public void DecorateRoom()
    {
        Debug.Log("Iniciando decoraci�n..."); // Verifica que el m�todo se llame

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
                    Debug.Log($"Encontrada posici�n v�lida en {spawnPos.Value}");
                    Quaternion rotation = CalculateRotation(group, spawnPos.Value);
                    GameObject prefab = group.prefabs[Random.Range(0, group.prefabs.Length)];
                    GameObject newObj = Instantiate(prefab, spawnPos.Value, rotation, transform);

                    // Ajusto altura post instanciado
                    Vector3 localPos = newObj.transform.localPosition; //cambiamos transform.position x localPosition para que acoma�e al padre
                    localPos.y = Random.Range(group.heightRange.x, group.heightRange.y);
                    newObj.transform.localPosition = localPos;

                    usedPositions.Add(spawnPos.Value);
                }
                else //agrego un else para ver donde esta el lio
                {
                    Debug.LogWarning("No se encontr� posici�n v�lida");
                }
            }
        }
    }

    private Vector3? FindValidSpawnPosition(SpawnableGroup group, List<Vector3> usedPositions)
    {
        int attempts = 50; // PAra evitar bucles infinitos

        while (attempts > 0)
        {
            //Genero direcci�n aleatoria
            Vector2 randomDir = Random.insideUnitCircle.normalized;

            //Calculo distancia entre min y max aleatoria
            float distance = Random.Range(group.minDistanceFromCenter, group.maxDistanceFromCenter);


            // Calcular posici�n aleatoria dentro del �rea espec�fica del grupo
            Vector3 localPos = new Vector3(
                randomDir.x * group.spawnAreaSize.x * 0.5f * distance,
                0,
                randomDir.y * group.spawnAreaSize.z * 0.5f * distance
            ) + group.spawnAreaOffset; //agrego aca el offset para que ya este aplicado despues

            // Convertir a posici�n global con offset considerando la rotaci�n del padre
            Vector3 worldPos = transform.TransformPoint(localPos);
            //pos.y = transform.position.y; //a ver si es necesario o no esto

            // Forzar alineaci�n con paredes si est� activado
            if (group.alignWithWalls)
            {
                worldPos = AlignToNearestWall(worldPos, group);
            }

            Vector3 checkPos = transform.InverseTransformPoint(worldPos) - roomOffset;

            // Verificar que est� dentro de los l�mites de la habitaci�n
            if (!IsInsideRoomBounds(checkPos))
            {
                attempts--;
                continue;
            }

            // Verificar superposici�n si es necesario
            if (!group.avoidOverlap || !CheckOverlap(worldPos, group.overlapRadius, usedPositions))
            {
                return worldPos;
            }

            attempts--;
        }

        Debug.LogWarning($"No se encontr� posici�n v�lida para {group.name} despu�s de 50 intentos");
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
        //agrego conversi�n a local
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

    // Visualizaci�n en el Editor
    private void OnDrawGizmosSelected()
    {
        if (!visualizeInEditor || spawnableGroups == null) return;

        // Dibujar l�mites de la habitaci�n
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Vector3 roomCenter = transform.position + roomOffset + Vector3.up * roomSize.y * 0.5f;
        Gizmos.DrawWireCube(roomCenter, roomSize);

        foreach (var group in spawnableGroups)
        {
            // Dibujar �rea de spawn del grupo
            Gizmos.color = group.gizmoColor;

            // Calcular posici�n del �rea de spawn con offset
            Vector3 spawnAreaCenter = transform.position + group.spawnAreaOffset;

            Vector3 spawnAreaVisualSize = new Vector3(group.spawnAreaSize.x, 
                                            group.spawnAreaSize.y > 0 ? group.spawnAreaSize.y : 0.1f, 
                                            group.spawnAreaSize.z);
            /*
            // Dibujar cubo que representa el �rea de spawn
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
            */ //C�digo Anterior
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