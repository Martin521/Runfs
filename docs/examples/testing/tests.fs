module Mylib.Tests

#r_package "xunit.v3.mtp-v2@3.2.2"
#r_project "mylib/mylib.fsproj"

#nowarn 988

open Xunit
open Mylib

[<Fact>]
let test1 () =
    Assert.Equal(x, 42)
