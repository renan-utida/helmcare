import pytest

from helmcare_cyber.pii import MotorPII


@pytest.fixture
def motor() -> MotorPII:
    return MotorPII()
