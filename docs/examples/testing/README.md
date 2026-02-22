# Using runfs for library testing

## Explorative testing

Just create an ad-hoc driver file (see example file `explore.fs` in this directory), add a project reference and start testing your library API by running `dotnet runfs explore.fs`.

## Unit test development / fixes

Rather than building a test project, discovering the tests, selecting the one you are interested in and running it, you just add the project reference and the test framework package reference to the test file you are interested in (see example file `tests.fs`, using xunit.v3) and run it with runfs (here: `dotnet runfs tests.fs`).

