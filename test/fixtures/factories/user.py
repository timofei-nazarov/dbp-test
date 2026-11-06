from factory import Faker

from main.models import User
from .base import ImageFileProvider, FactoryBase

Faker.add_provider(ImageFileProvider)


class UserFactory(FactoryBase):
    username = Faker("user_name")
    role = Faker("random_element", elements=User.Roles.values)
    email = Faker("email")
    first_name = Faker("first_name")
    last_name = Faker("last_name")
    avatar_picture = Faker("image_file", fmt="jpeg")
