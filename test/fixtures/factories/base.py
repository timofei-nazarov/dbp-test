from django.core.files.uploadedfile import SimpleUploadedFile
from factory import Factory
from faker.providers import BaseProvider


class ImageFileProvider(BaseProvider):
    def image_file(self, fmt: str = "jpeg") -> SimpleUploadedFile:
        return SimpleUploadedFile(
            self.generator.file_name(extension=fmt),
            self.generator.image(image_format=fmt),
        )


class FactoryBase(Factory):
    class Meta:
        model = dict
