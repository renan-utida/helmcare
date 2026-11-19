/** Raio médio da Terra, em km. */
const RAIO_TERRA_KM = 6371;

/** Converte graus em radianos. */
function radianos(graus: number): number {
  return (graus * Math.PI) / 180;
}

/**
 * Distância em linha reta entre dois pontos (fórmula de haversine), em km. Usada só no aparelho,
 * para ordenar os trechos críticos pela posição da pessoa, que nunca sai do celular.
 */
export function distanciaKm(latitudeA: number, longitudeA: number, latitudeB: number, longitudeB: number): number {
  const dLat = radianos(latitudeB - latitudeA);
  const dLon = radianos(longitudeB - longitudeA);
  const a =
    Math.sin(dLat / 2) ** 2 + Math.cos(radianos(latitudeA)) * Math.cos(radianos(latitudeB)) * Math.sin(dLon / 2) ** 2;
  return 2 * RAIO_TERRA_KM * Math.asin(Math.sqrt(a));
}
