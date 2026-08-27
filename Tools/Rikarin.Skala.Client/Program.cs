using Rikarin.Skala.Client;

// ⚠ No argument-parsing library, no host builder, no logging. Every type this file touches is a
// type the AOT compiler has to root and the loader has to page in before the process can answer,
// and the whole budget for the operation is 40 ms. See ThinClient's remarks.
return ThinClient.Run(args);
