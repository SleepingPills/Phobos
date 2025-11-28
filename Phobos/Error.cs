using System;

namespace Phobos;

public class PhobosError(string message) : Exception(message);