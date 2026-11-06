.PHONY: all test clean

test:
	coverage run -m pytest
	coverage lcov -o reports/coverage.lcov
