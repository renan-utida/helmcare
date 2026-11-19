"""Motor de parsing PII do HelmCare.

Uso típico:

    from helmcare_cyber.pii import MotorPII
    motor = MotorPII()
    achados = motor.varrer({"cpf": "..."})
    copia, achados = motor.mascarar({"cpf": "..."})
"""

from helmcare_cyber.pii.categorias import Categoria, Confianca, Metodo
from helmcare_cyber.pii.motor import Achado, MotorPII

__all__ = ["Achado", "Categoria", "Confianca", "Metodo", "MotorPII"]
