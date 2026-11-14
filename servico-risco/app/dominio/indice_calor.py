"""Índice de calor da NOAA, em graus Celsius (seção 6.1 do contrato).

É o porte exato da IndiceCalor.CalcularC da API em C#, que por sua vez reproduz a
computeHeatIndex(temperatura, umidade, false) da DHT sensor library (Adafruit),
usada no capacete. As três partes precisam dar o mesmo número, então as constantes
são copiadas sem simplificação (inclusive o 0.55555 no lugar de 5/9).
"""

import math


def calcular_c(temperatura_c: float, umidade_pct: float) -> float:
    """Calcula o índice de calor em °C a partir da temperatura (°C) e da umidade (%).

    A conta é feita em Fahrenheit: primeiro a fórmula simplificada de Steadman e,
    se o resultado passar de 79 °F, a regressão de Rothfusz com os dois ajustes de
    umidade da NOAA. O resultado volta para Celsius. Valores de referência do
    contrato: 33,4 °C e 58% dão 39,8 °C; 23,5 °C e 58% dão 23,4 °C.
    """
    t = temperatura_c * 1.8 + 32
    u = umidade_pct

    # Fórmula simplificada de Steadman, válida para índices baixos.
    indice = 0.5 * (t + 61.0 + ((t - 68.0) * 1.2) + (u * 0.094))

    if indice > 79:
        # Regressão de Rothfusz.
        indice = (
            -42.379
            + 2.04901523 * t
            + 10.14333127 * u
            - 0.22475541 * t * u
            - 0.00683783 * t * t
            - 0.05481717 * u * u
            + 0.00122874 * t * t * u
            + 0.00085282 * t * u * u
            - 0.00000199 * t * t * u * u
        )
        # Ajustes da NOAA para umidade muito baixa ou muito alta.
        if u < 13 and 80 <= t <= 112:
            indice -= (13 - u) * 0.25 * math.sqrt((17 - abs(t - 95)) * 0.05882)
        elif u > 85 and 80 <= t <= 87:
            indice += (u - 85) * 0.1 * ((87 - t) * 0.2)

    return (indice - 32) * 0.55555
