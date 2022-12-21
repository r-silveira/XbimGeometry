﻿
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Geometry.Abstractions;
using Xbim.Geometry.NetCore.Tests;
using Xbim.Geometry.WexBim;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO.Memory;
using Xunit;

namespace Xbim.Geometry.NetCore.Tests
{
    public class WexBimTests
    {
        private readonly IXShapeService _shapeService;
        private readonly IXGeometryConverterFactory _geomConverterFactory;
        private readonly ILoggerFactory _loggerFactory;

        public WexBimTests(IXShapeService shapeService, IXGeometryConverterFactory geomConverterFactory, ILoggerFactory loggerFactory)
        {
            _shapeService = shapeService;
            _geomConverterFactory = geomConverterFactory;
            _loggerFactory = loggerFactory;
        }

        [Fact]
        public void Can_read_and_write_block_as_wexbim()
        {
            var geomEngineV6 = _geomConverterFactory.CreateGeometryEngineV6(new MemoryModel(new Ifc4.EntityFactoryIfc4()), _loggerFactory);

            var blockMoq = IfcMoq.IfcBlockMoq() as IIfcCsgPrimitive3D;
            var solid = geomEngineV6.Build(blockMoq) as IXSolid; //initialise the factory with the block
            var meshFactors = geomEngineV6.MeshFactors.SetGranularity(MeshGranularity.Normal);
            IXAxisAlignedBoundingBox bounds;
            byte [] bytes = _shapeService.CreateWexBimMesh(solid, meshFactors, 0.001, out bounds);

            var wexBimMesh = new WexBimMesh(bytes);
            wexBimMesh.Vertices.Count().Should().Be(8);
            wexBimMesh.TriangleCount.Should().Be(12);
            wexBimMesh.FaceCount.Should().Be(6);
        }

        [Fact]
        public void Can_read_and_write_different_mesh_granularity()
        {
            var geomEngineV6 = _geomConverterFactory.CreateGeometryEngineV6(new MemoryModel(new Ifc4.EntityFactoryIfc4()), _loggerFactory);

            var sphereMoq = IfcMoq.IfcSphereMoq(radius: 500) as IIfcCsgPrimitive3D;
            var solid = geomEngineV6.Build(sphereMoq) as IXSolid; //initialise the factory with the block
            var meshFactors = geomEngineV6.MeshFactors.SetGranularity(MeshGranularity.Fine);
            IXAxisAlignedBoundingBox bounds;
            byte[] bytes = _shapeService.CreateWexBimMesh(solid, meshFactors, 0.001, out bounds);

            var wexBimMesh = new WexBimMesh(bytes);
           // wexBimMesh.Vertices.Count().Should().Be(8);
            wexBimMesh.TriangleCount.Should().Be(12);
            //wexBimMesh.FaceCount.Should().Be(6);
        }
    }
}
