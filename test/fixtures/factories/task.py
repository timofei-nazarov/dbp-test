from factory import Faker

from .base import FactoryBase


class TaskFactory(FactoryBase):
    title = Faker("text", max_nb_chars=20)
