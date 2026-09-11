using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDeathVfx : MonoBehaviour
{
    [SerializeField] ParticleSystem deathDotsPrefab;
    [SerializeField] float coverDuration = 0.35f;
    [SerializeField] float explodeSpeed = 22f;
    [SerializeField] float explodeDamping = 12f;
    [SerializeField] float explodeLifetime = 1f;
    [SerializeField] string deathStateName = "Death";

    ParticleSystem dots;
    SkinnedMeshRenderer[] bodyMeshes;
    bool built;

    public IEnumerator PlayRoutine(Animator animator)
    {
        EnsureBuilt();
        if (dots == null)
            yield break;

        foreach (var skin in bodyMeshes)
        {
            if (skin != null)
                skin.updateWhenOffscreen = true;
        }

        yield return WaitForDeathPose(animator);

        if (animator != null)
        {
            animator.speed = 0f;
            animator.Update(0f);
        }

        yield return new WaitForEndOfFrame();

        Mesh posed = BakeDeathPose();
        if (posed == null || posed.vertexCount == 0)
            yield break;

        var shape = dots.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Mesh;
        shape.mesh = posed;
        shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
        shape.normalOffset = 0.02f;

        dots.Clear(true);
        dots.Emit(dots.main.maxParticles);
        HideBody();

        yield return new WaitForSeconds(coverDuration);

        ExplodeDots();

        yield return DampenExplosion();
    }

    void EnsureBuilt()
    {
        if (built)
            return;

        built = true;
        bodyMeshes = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (FindBestMesh(bodyMeshes) == null)
            return;

        GameObject instance;
        if (deathDotsPrefab != null)
        {
            instance = Instantiate(deathDotsPrefab.gameObject, transform);
            instance.name = "DeathDots";
        }
        else
        {
            instance = new GameObject("DeathDots");
            instance.transform.SetParent(transform, false);
            instance.AddComponent<ParticleSystem>();
        }

        dots = instance.GetComponent<ParticleSystem>();
        dots.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dots.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = dots.emission;
        emission.rateOverTime = 0f;
    }

    Mesh BakeDeathPose()
    {
        var combine = new List<CombineInstance>();

        foreach (var skin in bodyMeshes)
        {
            if (skin == null || skin.sharedMesh == null)
                continue;

            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            if (baked.vertexCount == 0)
                continue;

            combine.Add(new CombineInstance
            {
                mesh = baked,
                transform = dots.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix
            });
        }

        if (combine.Count == 0)
            return null;

        var posed = new Mesh();
        posed.CombineMeshes(combine.ToArray(), true, true);
        return posed;
    }

    IEnumerator WaitForDeathPose(Animator animator)
    {
        if (animator == null)
            yield break;

        float timeout = 8f;
        float elapsed = 0f;
        while (elapsed < timeout)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            bool inDeath = current.IsName(deathStateName);
            bool goingToDeath = animator.IsInTransition(0) && next.IsName(deathStateName);
            float time = inDeath
                ? current.normalizedTime
                : (goingToDeath ? next.normalizedTime : 0f);

            if (inDeath && time >= 0.99f)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void ExplodeDots()
    {
        int max = Mathf.Max(dots.main.maxParticles, dots.particleCount);
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[max];
        int count = dots.GetParticles(particles);
        Vector3 origin = bodyMeshes.Length > 0 && bodyMeshes[0] != null
            ? bodyMeshes[0].bounds.center
            : transform.position;

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = particles[i].position - origin;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Random.onUnitSphere;
            else
                dir.Normalize();

            particles[i].velocity = dir * explodeSpeed * Random.Range(0.75f, 1.35f);
            particles[i].remainingLifetime = explodeLifetime + 0.4f;
            particles[i].startLifetime = explodeLifetime + 0.4f;
        }

        dots.SetParticles(particles, count);
        dots.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    IEnumerator DampenExplosion()
    {
        int max = Mathf.Max(dots.main.maxParticles, dots.particleCount);
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[max];
        float elapsed = 0f;

        while (elapsed < explodeLifetime)
        {
            float dt = Time.deltaTime;
            int count = dots.GetParticles(particles);
            float drag = Mathf.Exp(-explodeDamping * dt);

            for (int i = 0; i < count; i++)
                particles[i].velocity *= drag;

            dots.SetParticles(particles, count);
            elapsed += dt;
            yield return null;
        }
    }

    void HideBody()
    {
        foreach (var skin in bodyMeshes)
        {
            if (skin != null)
                skin.enabled = false;
        }
    }

    static SkinnedMeshRenderer FindBestMesh(SkinnedMeshRenderer[] meshes)
    {
        SkinnedMeshRenderer best = null;
        int bestVerts = -1;
        foreach (var mesh in meshes)
        {
            if (mesh == null || mesh.sharedMesh == null)
                continue;

            int verts = mesh.sharedMesh.vertexCount;
            if (verts > bestVerts)
            {
                best = mesh;
                bestVerts = verts;
            }
        }

        return best;
    }
}
