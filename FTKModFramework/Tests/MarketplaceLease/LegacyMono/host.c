#include <dlfcn.h>
#include <stdio.h>

int main(int argc, char **argv)
{
    if (argc != 5) return 2;
    void *library = dlopen(argv[1], RTLD_NOW | RTLD_GLOBAL);
    if (!library) { fprintf(stderr, "%s\n", dlerror()); return 3; }

    void (*set_paths)(const char *) = dlsym(library, "mono_set_assemblies_path");
    void *(*initialize)(const char *, const char *) = dlsym(library, "mono_jit_init_version");
    void *(*open_assembly)(void *, const char *) = dlsym(library, "mono_domain_assembly_open");
    int (*execute)(void *, void *, int, char **) = dlsym(library, "mono_jit_exec");
    if (!set_paths || !initialize || !open_assembly || !execute) {
        fprintf(stderr, "Required Mono embedding entrypoints are unavailable\n");
        return 4;
    }
    set_paths(argv[2]);
    void *domain = initialize("lease-regression", "v2.0.50727");
    if (!domain) return 5;
    void *assembly = open_assembly(domain, argv[3]);
    if (!assembly) return 6;
    char *arguments[] = { argv[3], argv[4] };
    return execute(domain, assembly, 2, arguments);
}
